using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Compone le serie storiche della panoramica: investimenti dal servizio dedicato e conti
/// dalle rilevazioni manuali. I conti senza rilevazione non vengono trattati come zero:
/// il totale storico è la somma dei saldi disponibili e viene dichiarato parziale.
/// </summary>
public sealed class DashboardOverviewService
{
    private static readonly Currency Euro = Currency.Parse("EUR");

    private readonly InvestmentOverviewService _investments;
    private readonly IAccountRepository _accounts;
    private readonly IManualBalanceRepository _manualBalances;
    private readonly Func<DateOnly> _currentUtcDate;

    public DashboardOverviewService(
        InvestmentOverviewService investments,
        IAccountRepository accounts,
        IManualBalanceRepository manualBalances,
        Func<DateOnly>? currentUtcDateProvider = null)
    {
        _investments = investments;
        _accounts = accounts;
        _manualBalances = manualBalances;
        _currentUtcDate = currentUtcDateProvider ?? (() => DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<DashboardOverview> LoadAsync(CancellationToken cancellationToken = default)
    {
        var valuationDate = _currentUtcDate();
        var investments = await _investments.LoadAsync(cancellationToken);
        var accounts = await _accounts.ListAsync(cancellationToken);
        var readings = await _manualBalances.ListAsync(cancellationToken);

        return new DashboardOverview(
            valuationDate,
            BuildInvestments(investments),
            BuildAccounts(accounts, readings, valuationDate));
    }

    private static DashboardOverviewCategory BuildInvestments(InvestmentOverview overview)
    {
        var points = overview.History.Points
            .Select(point => new DashboardOverviewPoint(
                point.Date,
                point.CurrentValue,
                point.InvestedCapital,
                point.CurrentValue is not null && point.InvestedCapital is not null))
            .ToArray();

        Money? currentChange = overview.CurrentValue is { } current &&
                               overview.InvestedCapital is { } invested
            ? current - invested
            : null;

        var reasons = overview.IncompletenessReasons
            .Concat(overview.History.IncompletenessReasons)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new DashboardOverviewCategory(
            "investments",
            "Investimenti",
            Euro,
            "Valore attuale",
            "Capitale investito",
            overview.CurrentValue,
            overview.InvestedCapital,
            currentChange,
            overview.PerformancePercent,
            points,
            overview.IsComplete,
            !overview.History.IsComplete,
            reasons);
    }

    private static DashboardOverviewCategory BuildAccounts(
        IReadOnlyList<Account> accounts,
        IReadOnlyList<ManualBalance> readings,
        DateOnly valuationDate)
    {
        var dates = readings
            .Select(reading => reading.ValidAt)
            .Where(date => date <= valuationDate)
            .Distinct()
            .OrderBy(date => date)
            .ToList();

        if (!dates.Contains(valuationDate))
        {
            dates.Add(valuationDate);
        }

        var points = new List<DashboardOverviewPoint>(dates.Count);
        var reasons = new HashSet<string>(StringComparer.Ordinal);
        var hasPartialHistory = false;
        var currentComplete = false;

        foreach (var date in dates)
        {
            var isCurrent = date == valuationDate;
            var total = 0m;
            var covered = 0;

            foreach (var account in accounts)
            {
                var reading = ManualBalances.LatestOnOrBefore(readings, account.Id, date);
                if (reading is null)
                {
                    continue;
                }

                covered++;
                total += reading.Amount.Amount;
            }

            var complete = accounts.Count > 0 && covered == accounts.Count;
            if (!complete)
            {
                hasPartialHistory = true;
            }

            if (isCurrent)
            {
                currentComplete = complete;
                if (accounts.Count == 0)
                {
                    reasons.Add("Nessun conto registrato.");
                }
                else if (covered < accounts.Count)
                {
                    reasons.Add(
                        $"Solo {covered} conti su {accounts.Count} hanno una rilevazione corrente.");
                }
            }

            Money? totalMoney = covered > 0 ? new Money(total, Euro) : null;
            points.Add(new DashboardOverviewPoint(date, totalMoney, null, complete));
        }

        var currentPoint = points[^1];

        return new DashboardOverviewCategory(
            "accounts",
            "Conti",
            Euro,
            "Totale conti",
            "",
            currentComplete ? currentPoint.Value : null,
            null,
            null,
            null,
            points,
            currentComplete,
            hasPartialHistory,
            reasons.ToArray());
    }

}
