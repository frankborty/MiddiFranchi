using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Compone la dashboard riutilizzando i servizi esistenti. Non introduce un secondo calcolatore
/// del patrimonio: la valorizzazione resta responsabilità di <see cref="NetWorthOverviewService"/>.
/// </summary>
public sealed class FinancialDashboardService
{
    private const int RecentMovementsCount = 6;

    private readonly NetWorthOverviewService _netWorth;
    private readonly AccountOverviewService _accounts;
    private readonly MovementService _movements;
    private readonly Func<DateOnly> _currentUtcDate;

    public FinancialDashboardService(
        NetWorthOverviewService netWorth,
        AccountOverviewService accounts,
        MovementService movements,
        Func<DateOnly>? currentUtcDateProvider = null)
    {
        _netWorth = netWorth;
        _accounts = accounts;
        _movements = movements;
        _currentUtcDate = currentUtcDateProvider ?? (() => DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<FinancialDashboard> LoadAsync(CancellationToken cancellationToken = default)
    {
        var today = _currentUtcDate();
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var netWorth = await _netWorth.SummarizeAsync(today, cancellationToken);
        var accounts = await _accounts.ListAsync(cancellationToken);
        var postings = await _movements.ListAsync(null, null, today, cancellationToken);
        var monthPostings = postings
            .Where(posting => posting.OperationDate >= monthStart)
            .ToArray();

        return new FinancialDashboard(
            netWorth,
            accounts,
            BuildMonthSummary(monthStart, today, monthPostings),
            BuildRecentMovements(postings, accounts),
            BuildInvestments(netWorth));
    }

    private static DashboardMonthSummary BuildMonthSummary(
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyList<AccountPosting> monthPostings)
    {
        var operations = monthPostings
            .GroupBy(posting => posting.OperationId)
            .Select(group => new
            {
                Kind = group.First().Operation.Kind,
                Currency = group.First().Currency,
                SignedAmount = group.Sum(posting => posting.SignedAmount),
            })
            .ToArray();

        var flows = operations
            .Where(operation => operation.Kind is OperationKind.Income or OperationKind.Expense)
            .GroupBy(operation => operation.Currency)
            .Select(group => new DashboardCurrencyFlow(
                group.Key,
                group.Where(operation => operation.SignedAmount > 0m).Sum(operation => operation.SignedAmount),
                group.Where(operation => operation.SignedAmount < 0m).Sum(operation => -operation.SignedAmount),
                group.Count()))
            .OrderBy(flow => flow.Currency.Code != "EUR")
            .ThenBy(flow => flow.Currency.Code, StringComparer.Ordinal)
            .ToArray();

        var incomeExpenseCount = operations.Count(operation =>
            operation.Kind is OperationKind.Income or OperationKind.Expense);

        return new DashboardMonthSummary(
            periodStart,
            periodEnd,
            flows,
            operations.Length,
            incomeExpenseCount,
            operations.Length - incomeExpenseCount);
    }

    private static IReadOnlyList<DashboardMovement> BuildRecentMovements(
        IReadOnlyList<AccountPosting> postings,
        IReadOnlyList<AccountOverviewRow> accounts)
    {
        var accountNames = accounts.ToDictionary(account => account.AccountId, account => account.Name);

        return postings
            .GroupBy(posting => posting.OperationId)
            .Select(group => new
            {
                Movement = BuildMovement(group, accountNames),
                RecordedAt = group.First().Operation.RecordedAt,
            })
            .OrderByDescending(item => item.Movement.Date)
            .ThenByDescending(item => item.RecordedAt)
            .ThenByDescending(item => item.Movement.OperationId)
            .Take(RecentMovementsCount)
            .Select(item => item.Movement)
            .ToArray();
    }

    private static DashboardMovement BuildMovement(
        IGrouping<Guid, AccountPosting> postings,
        IReadOnlyDictionary<Guid, string> accountNames)
    {
        var operation = postings.First().Operation;

        return operation switch
        {
            Income income => new DashboardMovement(
                income.Id,
                income.OperationDate,
                OperationKind.Income,
                income.Description,
                income.Category.Name,
                income.Account.Name,
                new Money(postings.Sum(posting => posting.SignedAmount), income.Account.Currency),
                null),
            Expense expense => new DashboardMovement(
                expense.Id,
                expense.OperationDate,
                OperationKind.Expense,
                expense.Description,
                expense.Category.Name,
                expense.Account.Name,
                new Money(postings.Sum(posting => posting.SignedAmount), expense.Account.Currency),
                null),
            Transfer transfer => new DashboardMovement(
                transfer.Id,
                transfer.OperationDate,
                OperationKind.Transfer,
                transfer.Description,
                null,
                $"{transfer.SourceAccount.Name} → {transfer.DestinationAccount.Name}",
                new Money(-transfer.SourceAmount.Value.Amount, transfer.SourceAmount.Value.Currency),
                new Money(
                    transfer.DestinationAmount.Value.Amount,
                    transfer.DestinationAmount.Value.Currency)),
            InvestmentOperation investment => new DashboardMovement(
                investment.Id,
                investment.OperationDate,
                OperationKind.Investment,
                investment.Description,
                CategoryLabel(investment),
                investment.SettlementAccount.Name,
                InvestmentAmount(postings),
                null),
            _ => new DashboardMovement(
                operation.Id,
                operation.OperationDate,
                operation.Kind,
                operation.Description,
                CategoryLabel(operation),
                accountNames.GetValueOrDefault(postings.First().AccountId, "Conto"),
                new Money(postings.Sum(posting => posting.SignedAmount), postings.First().Currency),
                null),
        };
    }

    private static Money? InvestmentAmount(IEnumerable<AccountPosting> postings)
    {
        var list = postings.ToArray();
        if (list.Length == 0)
        {
            return null;
        }

        var currency = list[0].Currency;
        return new Money(
            list.Where(posting => posting.Currency == currency).Sum(posting => posting.SignedAmount),
            currency);
    }

    private static DashboardInvestments BuildInvestments(NetWorthSummary summary)
    {
        var portfolios = summary.Investments
            .Select(BuildPortfolio)
            .ToArray();

        var total = portfolios.Any(portfolio => portfolio.HasValuedPositions)
            ? summary.InvestmentsSubtotal
            : null;

        return new DashboardInvestments(
            total,
            portfolios.Sum(portfolio => portfolio.ValuedPositionCount),
            portfolios.Sum(portfolio => portfolio.UnvaluedPositionCount),
            portfolios);
    }

    private static DashboardPortfolio BuildPortfolio(PortfolioValuation valuation)
    {
        var positions = valuation.Positions
            .Select(position => new DashboardInvestmentPosition(
                position.Position.Instrument.Name,
                position.Position.Instrument.Symbol,
                position.Value,
                position.IsValuated,
                position.IncompletenessReason))
            .ToArray();

        var valuedPositions = positions
            .Where(position => position.IsValuated)
            .ToArray();

        Money? total = valuation.IsComplete && valuedPositions.Length > 0
            ? new Money(
                valuedPositions.Sum(position => position.Value!.Value.Amount),
                valuation.Portfolio.ReferenceCurrency)
            : null;

        var distribution = total is { Amount: > 0m } portfolioTotal
            ? valuedPositions
                .Select(position => new DashboardInvestmentShare(
                    position.InstrumentName,
                    position.Value!.Value,
                    (double)(position.Value.Value.Amount / portfolioTotal.Amount)))
                .OrderByDescending(item => item.Share)
                .ToArray()
            : [];

        return new DashboardPortfolio(
            valuation.Portfolio.Id,
            valuation.Portfolio.Name,
            valuation.Portfolio.ReferenceCurrency,
            total,
            positions,
            distribution);
    }

    private static string? CategoryLabel(FinancialOperation operation) => operation switch
    {
        Income income => income.Category.Name,
        Expense expense => expense.Category.Name,
        InvestmentOperation { InvestmentKind: InvestmentOperationKind.StandaloneFee } => "Commissione",
        InvestmentOperation investment => investment.Instrument?.Name,
        _ => null,
    };
}
