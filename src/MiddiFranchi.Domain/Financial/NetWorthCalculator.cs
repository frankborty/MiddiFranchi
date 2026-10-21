using System.Globalization;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

/// <summary>
/// Calcola il patrimonio netto in EUR. Tutti gli importi dell'applicazione sono in EUR:
/// non esistono conversioni di valuta. Le collezioni di conti e portafogli definiscono il
/// perimetro richiesto: il Domain non possiede un aggregate root del perimetro completo.
/// La modalità è una scelta esplicita del chiamante: passare le rilevazioni manuali seleziona
/// i saldi manuali come fonte autorevole dei conti — un conto senza rilevazione valida alla
/// data è incompleto e non diventa zero — mentre ometterle seleziona la modalità posting.
/// </summary>
public static class NetWorthCalculator
{
    private static readonly Currency Euro = Currency.Euro;

    public static NetWorthSummary Summarize(
        IReadOnlyCollection<Account> accounts,
        IEnumerable<AccountPosting> postings,
        IEnumerable<InvestmentPortfolio> portfolios,
        IEnumerable<InvestmentOperation> operations,
        IEnumerable<PriceObservation> priceObservations,
        DateOnly valuationDate,
        ValuationPolicy? valuationPolicy = null,
        IReadOnlyList<ManualBalance>? manualBalances = null)
    {
        if (accounts is null) throw new ArgumentNullException(nameof(accounts));
        if (postings is null) throw new ArgumentNullException(nameof(postings));
        if (portfolios is null) throw new ArgumentNullException(nameof(portfolios));
        if (operations is null) throw new ArgumentNullException(nameof(operations));
        if (priceObservations is null) throw new ArgumentNullException(nameof(priceObservations));

        var accountList = accounts.ToArray();
        EnsureDistinctAccountIds(accountList);

        var postingList = postings as IReadOnlyCollection<AccountPosting> ?? postings.ToArray();
        var portfolioList = portfolios.ToArray();
        EnsureDistinctPortfolioIds(portfolioList);

        var operationList = operations.ToArray();
        var priceList = priceObservations as IReadOnlyCollection<PriceObservation> ?? priceObservations.ToArray();
        var policy = valuationPolicy ?? ValuationPolicy.Unbounded;
        var incompletenessReasons = new List<string>();

        var accountEntries = new List<AccountBalanceEntry>();
        foreach (var account in accountList)
        {
            Money? balance;
            if (manualBalances is not null)
            {
                var reading = ManualBalances.LatestOnOrBefore(manualBalances, account.Id, valuationDate);
                if (reading is null)
                {
                    incompletenessReasons.Add(
                        $"Il conto {account.Name} non ha una rilevazione manuale valida alla data {FormatDate(valuationDate)}: il saldo corrente non è disponibile.");
                    accountEntries.Add(new AccountBalanceEntry(
                        account,
                        null,
                        "Rilevazione manuale del saldo assente alla data richiesta."));
                    continue;
                }

                balance = reading.Amount;
            }
            else
            {
                balance = AccountBalanceCalculator.Balance(account, postingList, valuationDate);
            }

            accountEntries.Add(new AccountBalanceEntry(account, balance, null));
        }

        var accountsSubtotal = accountEntries.All(entry => entry.IsComplete)
            ? new Money(accountEntries.Sum(entry => entry.Balance!.Value.Amount), Euro)
            : (Money?)null;

        var portfolioValuations = portfolioList
            .Select(portfolio => PortfolioValuationCalculator.Valuate(
                portfolio, operationList, priceList, valuationDate, policy))
            .ToList();

        foreach (var valuation in portfolioValuations.Where(valuation => valuation.IsIncomplete))
        {
            foreach (var position in valuation.Positions.Where(position => position.IsIncomplete))
            {
                incompletenessReasons.Add(
                    $"Portafoglio {valuation.Portfolio.Name}: {position.IncompletenessReason}");
            }
        }

        var investmentsSubtotal = portfolioValuations.All(valuation => valuation.IsComplete)
            ? new Money(
                portfolioValuations.Sum(valuation =>
                    valuation.Positions.Sum(position => position.Value!.Value.Amount)),
                Euro)
            : (Money?)null;

        var totalValue = accountsSubtotal is not null && investmentsSubtotal is not null
            ? new Money(accountsSubtotal.Value.Amount + investmentsSubtotal.Value.Amount, Euro)
            : (Money?)null;

        return new NetWorthSummary(
            valuationDate,
            Euro,
            accountEntries,
            portfolioValuations,
            accountsSubtotal,
            investmentsSubtotal,
            totalValue,
            incompletenessReasons);
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

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
