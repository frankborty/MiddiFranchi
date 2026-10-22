using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Compone la dashboard riutilizzando i servizi esistenti. Non introduce un secondo calcolatore
/// del patrimonio: la valorizzazione resta responsabilità di <see cref="NetWorthOverviewService"/>.
/// I movimenti ordinari non dipendono più dai posting: entrate e uscite non hanno conto.
/// </summary>
public sealed class FinancialDashboardService
{
    private const int RecentMovementsCount = 6;

    private readonly NetWorthOverviewService _netWorth;
    private readonly AccountOverviewService _accounts;
    private readonly IFinancialOperationRepository _operations;
    private readonly Func<DateOnly> _currentUtcDate;

    public FinancialDashboardService(
        NetWorthOverviewService netWorth,
        AccountOverviewService accounts,
        IFinancialOperationRepository operations,
        Func<DateOnly>? currentUtcDateProvider = null)
    {
        _netWorth = netWorth;
        _accounts = accounts;
        _operations = operations;
        _currentUtcDate = currentUtcDateProvider ?? (() => DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<FinancialDashboard> LoadAsync(CancellationToken cancellationToken = default)
    {
        var today = _currentUtcDate();
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var netWorth = await _netWorth.SummarizeAsync(today, cancellationToken);
        var accounts = await _accounts.ListAsync(cancellationToken);
        var operations = await _operations.ListAsync(cancellationToken);

        return new FinancialDashboard(
            netWorth,
            accounts,
            BuildMonthSummary(monthStart, today, operations),
            BuildRecentMovements(operations),
            BuildInvestments(netWorth));
    }

    private static DashboardMonthSummary BuildMonthSummary(
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyList<FinancialOperation> operations)
    {
        var monthOperations = operations
            .Where(operation => operation.Status == OperationStatus.Registered)
            .Where(operation => operation.OperationDate >= periodStart && operation.OperationDate <= periodEnd)
            .ToArray();

        var flows = monthOperations
            .Where(operation => operation is Income or Expense)
            .GroupBy(CurrencyOf)
            .Select(group => new DashboardCurrencyFlow(
                group.Key,
                group.OfType<Income>().Sum(income => income.Amount.Value.Amount),
                group.OfType<Expense>().Sum(expense => expense.Amount.Value.Amount),
                group.Count()))
            .OrderBy(flow => flow.Currency.Code != "EUR")
            .ThenBy(flow => flow.Currency.Code, StringComparer.Ordinal)
            .ToArray();

        var incomeExpenseCount = monthOperations.Count(operation =>
            operation is Income or Expense);

        return new DashboardMonthSummary(
            periodStart,
            periodEnd,
            flows,
            monthOperations.Length,
            incomeExpenseCount,
            monthOperations.Length - incomeExpenseCount);
    }

    private static IReadOnlyList<DashboardMovement> BuildRecentMovements(
        IReadOnlyList<FinancialOperation> operations) =>
        operations
            .Where(operation => operation.Status == OperationStatus.Registered)
            .Where(operation => operation.Kind is OperationKind.Income or OperationKind.Expense or OperationKind.Transfer)
            .OrderByDescending(operation => operation.OperationDate)
            .ThenByDescending(operation => operation.RecordedAt)
            .ThenByDescending(operation => operation.Id)
            .Take(RecentMovementsCount)
            .Select(ToDashboardMovement)
            .ToArray();

    private static DashboardMovement ToDashboardMovement(FinancialOperation operation) => operation switch
    {
        Income income => new DashboardMovement(
            income.Id,
            income.OperationDate,
            OperationKind.Income,
            income.Description,
            income.Category.Name,
            income.Category.Subcategory,
            income.Category.Icon,
            income.Category.ColorHex,
            income.Account?.Name,
            income.Amount.Value,
            null),
        Expense expense => new DashboardMovement(
            expense.Id,
            expense.OperationDate,
            OperationKind.Expense,
            expense.Description,
            expense.Category.Name,
            expense.Category.Subcategory,
            expense.Category.Icon,
            expense.Category.ColorHex,
            expense.Account?.Name,
            new Money(-expense.Amount.Value.Amount, expense.Amount.Value.Currency),
            null),
        Transfer transfer => new DashboardMovement(
            transfer.Id,
            transfer.OperationDate,
            OperationKind.Transfer,
            transfer.Description,
            null,
            null,
            "transfer",
            null,
            $"{transfer.SourceAccount.Name} → {transfer.DestinationAccount.Name}",
            new Money(-transfer.SourceAmount.Value.Amount, transfer.SourceAmount.Value.Currency),
            transfer.DestinationAmount.Value),
        _ => throw new InvalidOperationException(
            $"Il tipo di operazione {operation.Kind} non è supportato dalla dashboard."),
    };

    private static Currency CurrencyOf(FinancialOperation operation) => operation switch
    {
        Income income => income.Amount.Value.Currency,
        Expense expense => expense.Amount.Value.Currency,
        _ => Currency.Euro,
    };

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
}
