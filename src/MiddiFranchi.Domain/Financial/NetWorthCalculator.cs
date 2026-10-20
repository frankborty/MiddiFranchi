using System.Globalization;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public static class NetWorthCalculator
{
    private static readonly Currency ReportingCurrency = Currency.Parse("EUR");

    /// <summary>
    /// Calcola il patrimonio netto in EUR. Le collezioni di conti e portafogli definiscono il perimetro
    /// richiesto: il Domain non possiede un aggregate root del perimetro completo e non può rilevare
    /// entità omesse dal chiamante. Allo stesso modo, i posting devono essere l'insieme canonico del
    /// registro: eventuali duplicati o omissioni non sono distinguibili da movimenti legittimi.
    /// </summary>
    public static NetWorthSummary Summarize(
        IReadOnlyCollection<Account> accounts,
        IEnumerable<AccountPosting> postings,
        IEnumerable<InvestmentPortfolio> portfolios,
        IEnumerable<InvestmentOperation> operations,
        IEnumerable<PriceObservation> priceObservations,
        IEnumerable<ExchangeRateObservation> exchangeRateObservations,
        DateOnly valuationDate,
        ValuationPolicy? valuationPolicy = null)
    {
        if (accounts is null) throw new ArgumentNullException(nameof(accounts));
        if (postings is null) throw new ArgumentNullException(nameof(postings));
        if (portfolios is null) throw new ArgumentNullException(nameof(portfolios));
        if (operations is null) throw new ArgumentNullException(nameof(operations));
        if (priceObservations is null) throw new ArgumentNullException(nameof(priceObservations));
        if (exchangeRateObservations is null) throw new ArgumentNullException(nameof(exchangeRateObservations));

        var accountList = accounts.ToArray();
        EnsureDistinctAccountIds(accountList);

        var postingList = postings as IReadOnlyCollection<AccountPosting> ?? postings.ToArray();
        var portfolioList = portfolios.ToArray();
        EnsureDistinctPortfolioIds(portfolioList);

        var operationList = operations.ToArray();
        var priceList = priceObservations as IReadOnlyCollection<PriceObservation> ?? priceObservations.ToArray();
        var exchangeRateList = exchangeRateObservations as IReadOnlyCollection<ExchangeRateObservation>
            ?? exchangeRateObservations.ToArray();
        var policy = valuationPolicy ?? ValuationPolicy.Unbounded;
        var incompletenessReasons = new List<string>();

        var accountEntries = new List<AccountBalanceEntry>();
        foreach (var account in accountList)
        {
            var balance = AccountBalanceCalculator.Balance(account, postingList, valuationDate);

            Money? converted = balance;
            ExchangeRateObservation? exchangeRateObservation = null;
            string? reason = null;

            if (account.Currency != ReportingCurrency)
            {
                exchangeRateObservation = ExchangeRateObservations
                    .MostRecentOnOrBefore(exchangeRateList, account.Currency, valuationDate);

                if (exchangeRateObservation is null)
                {
                    reason = $"Il conto {account.Name} ({account.Currency.Code}) richiede un cambio con valuta estera {account.Currency.Code}, ma nessuna osservazione valida è disponibile alla data richiesta.";
                    converted = null;
                }
                else if (policy.IsExchangeRateStale(exchangeRateObservation, valuationDate))
                {
                    reason = $"Il conto {account.Name} ({account.Currency.Code}) richiede un cambio obsoleto: ValidAt {FormatDate(exchangeRateObservation.ValidAt)}, età {AgeInDays(exchangeRateObservation.ValidAt, valuationDate)} giorni, soglia {policy.MaximumExchangeRateAgeDays} giorni.";
                    converted = null;
                }
                else
                {
                    converted = exchangeRateObservation.Rate.Convert(balance, ReportingCurrency);
                }
            }

            if (reason is not null)
            {
                incompletenessReasons.Add(reason);
            }

            accountEntries.Add(new AccountBalanceEntry(account, balance, converted, exchangeRateObservation, reason));
        }

        var accountsSubtotal = accountEntries.All(entry => entry.IsComplete)
            ? new Money(accountEntries.Sum(entry => entry.ConvertedBalance!.Value.Amount), ReportingCurrency)
            : (Money?)null;

        var portfolioValuations = portfolioList
            .Select(portfolio => PortfolioValuationCalculator
                .Valuate(portfolio, operationList, priceList, exchangeRateList, valuationDate, policy))
            .ToList();

        var investmentsSubtotal = SumInvestments(
            portfolioValuations, exchangeRateList, valuationDate, policy, incompletenessReasons);

        var totalValue = accountsSubtotal is not null && investmentsSubtotal is not null
            ? new Money(accountsSubtotal.Value.Amount + investmentsSubtotal.Value.Amount, ReportingCurrency)
            : (Money?)null;

        return new NetWorthSummary(
            valuationDate,
            ReportingCurrency,
            accountEntries,
            portfolioValuations,
            accountsSubtotal,
            investmentsSubtotal,
            totalValue,
            incompletenessReasons);
    }

    private static Money? SumInvestments(
        IReadOnlyList<PortfolioValuation> portfolioValuations,
        IReadOnlyCollection<ExchangeRateObservation> exchangeRateObservations,
        DateOnly valuationDate,
        ValuationPolicy policy,
        List<string> incompletenessReasons)
    {
        var contributions = new List<Money>();
        foreach (var valuation in portfolioValuations)
        {
            if (!valuation.IsComplete)
            {
                foreach (var position in valuation.Positions.Where(position => position.IsIncomplete))
                {
                    incompletenessReasons.Add($"Portafoglio {valuation.Portfolio.Name}: {position.IncompletenessReason}");
                }

                continue;
            }

            // Ogni posizione valutata è espressa nella valuta di riferimento del portafoglio (docs §7.3/7.4).
            var portfolioTotal = new Money(
                valuation.Positions.Sum(position => position.Value!.Value.Amount),
                valuation.Portfolio.ReferenceCurrency);

            if (portfolioTotal.Currency == ReportingCurrency)
            {
                contributions.Add(portfolioTotal);
                continue;
            }

            var exchangeRateObservation = ExchangeRateObservations
                .MostRecentOnOrBefore(exchangeRateObservations, portfolioTotal.Currency, valuationDate);
            if (exchangeRateObservation is null)
            {
                incompletenessReasons.Add(
                    $"Portafoglio {valuation.Portfolio.Name} ({portfolioTotal.Currency.Code}): nessuna osservazione di cambio valida alla data richiesta.");
                continue;
            }

            if (policy.IsExchangeRateStale(exchangeRateObservation, valuationDate))
            {
                incompletenessReasons.Add(
                    $"Portafoglio {valuation.Portfolio.Name} ({portfolioTotal.Currency.Code}): cambio obsoleto, ValidAt {FormatDate(exchangeRateObservation.ValidAt)}, età {AgeInDays(exchangeRateObservation.ValidAt, valuationDate)} giorni, soglia {policy.MaximumExchangeRateAgeDays} giorni.");
                continue;
            }

            contributions.Add(exchangeRateObservation.Rate.Convert(portfolioTotal, ReportingCurrency));
        }

        return contributions.Count == portfolioValuations.Count
            ? new Money(contributions.Sum(part => part.Amount), ReportingCurrency)
            : null;
    }

    private static void EnsureDistinctAccountIds(IReadOnlyList<Account> accounts)
    {
        var seen = new HashSet<Guid>();
        foreach (var account in accounts)
        {
            if (account is null)
            {
                throw new ArgumentNullException(nameof(accounts), "La collezione dei conti non può contenere null.");
            }

            if (!seen.Add(account.Id))
            {
                throw new ArgumentException(
                    $"Il conto {account.Name} ({account.Id}) è presente più volte: la stessa entità non può essere conteggiata due volte.",
                    nameof(accounts));
            }
        }
    }

    private static void EnsureDistinctPortfolioIds(IReadOnlyList<InvestmentPortfolio> portfolios)
    {
        var seen = new HashSet<Guid>();
        foreach (var portfolio in portfolios)
        {
            if (portfolio is null)
            {
                throw new ArgumentNullException(nameof(portfolios), "La collezione dei portafogli non può contenere null.");
            }

            if (!seen.Add(portfolio.Id))
            {
                throw new ArgumentException(
                    $"Il portafoglio {portfolio.Name} ({portfolio.Id}) è presente più volte: la stessa entità non può essere conteggiata due volte.",
                    nameof(portfolios));
            }
        }
    }

    private static int AgeInDays(DateOnly validAt, DateOnly valuationDate) =>
        valuationDate.DayNumber - validAt.DayNumber;

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
