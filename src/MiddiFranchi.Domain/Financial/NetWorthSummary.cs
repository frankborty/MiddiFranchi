using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed record NetWorthSummary
{
    public NetWorthSummary(
        DateOnly valuationDate,
        Currency reportingCurrency,
        IReadOnlyList<AccountBalanceEntry> accounts,
        IReadOnlyList<PortfolioValuation> investments,
        Money? accountsSubtotal,
        Money? investmentsSubtotal,
        Money? totalValue,
        IReadOnlyList<string> incompletenessReasons)
    {
        if (accounts is null)
        {
            throw new ArgumentNullException(nameof(accounts));
        }

        if (investments is null)
        {
            throw new ArgumentNullException(nameof(investments));
        }

        if (incompletenessReasons is null)
        {
            throw new ArgumentNullException(nameof(incompletenessReasons));
        }

        if (reportingCurrency.Code is null)
        {
            throw new ArgumentException("La valuta di rendicontazione è obbligatoria.", nameof(reportingCurrency));
        }

        ValuationDate = valuationDate;
        ReportingCurrency = reportingCurrency;
        Accounts = accounts.ToArray();
        Investments = investments.ToArray();
        AccountsSubtotal = accountsSubtotal;
        InvestmentsSubtotal = investmentsSubtotal;
        TotalValue = totalValue;
        IncompletenessReasons = incompletenessReasons.ToArray();
    }

    public DateOnly ValuationDate { get; }

    public Currency ReportingCurrency { get; }

    public IReadOnlyList<AccountBalanceEntry> Accounts { get; }

    public IReadOnlyList<PortfolioValuation> Investments { get; }

    public Money? AccountsSubtotal { get; }

    public Money? InvestmentsSubtotal { get; }

    public Money? TotalValue { get; }

    public IReadOnlyList<string> IncompletenessReasons { get; }

    public bool IsComplete => TotalValue is not null;
}
