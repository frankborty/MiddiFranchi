using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

public enum DashboardOverviewRange
{
    OneDay,
    SevenDays,
    OneMonth,
    YearToDate,
    OneYear,
    ThreeYears,
    Max,
}

public sealed record DashboardOverview(
    DateOnly ValuationDate,
    DashboardOverviewCategory Investments,
    DashboardOverviewCategory Accounts);

public sealed record DashboardOverviewCategory(
    string Key,
    string Label,
    Currency Currency,
    string ValueLabel,
    string ReferenceLabel,
    Money? CurrentValue,
    Money? CurrentReference,
    Money? CurrentChange,
    decimal? CurrentChangePercent,
    IReadOnlyList<DashboardOverviewPoint> Points,
    bool IsComplete,
    bool HasPartialHistory,
    IReadOnlyList<string> IncompletenessReasons)
{
    public DashboardOverviewWindow Window(DashboardOverviewRange range, DateOnly asOfDate)
    {
        var from = StartOf(range, asOfDate);
        var points = Points
            .Where(point => point.Date >= from)
            .ToList();

        if (points.Count == 0 && Points.Count > 0)
        {
            points.Add(Points[^1]);
        }
        else if (points.Count == 1)
        {
            var previous = Points.LastOrDefault(point => point.Date < points[0].Date);
            if (previous is not null)
            {
                points.Insert(0, previous);
            }
        }

        var firstValue = points.Count > 0 ? points[0].Value : null;
        var lastValue = points.Count > 0 ? points[^1].Value : null;
        Money? change = firstValue is { } first && lastValue is { } last
            ? last - first
            : null;
        decimal? changePercent = change is { } changeMoney &&
                                 firstValue is { Amount: > 0m } firstPositive
            ? changeMoney.Amount / firstPositive.Amount
            : null;

        return new DashboardOverviewWindow(
            points.ToArray(),
            firstValue,
            lastValue,
            change,
            changePercent,
            points.Count > 0 ? points[^1].Reference : null);
    }

    private static DateOnly StartOf(DashboardOverviewRange range, DateOnly asOfDate) => range switch
    {
        DashboardOverviewRange.OneDay => asOfDate.AddDays(-1),
        DashboardOverviewRange.SevenDays => asOfDate.AddDays(-7),
        DashboardOverviewRange.OneMonth => asOfDate.AddDays(-30),
        DashboardOverviewRange.YearToDate => new DateOnly(asOfDate.Year, 1, 1),
        DashboardOverviewRange.OneYear => asOfDate.AddDays(-365),
        DashboardOverviewRange.ThreeYears => asOfDate.AddDays(-1095),
        _ => DateOnly.MinValue,
    };
}

public sealed record DashboardOverviewPoint(
    DateOnly Date,
    Money? Value,
    Money? Reference,
    bool IsComplete = true);

public sealed record DashboardOverviewWindow(
    IReadOnlyList<DashboardOverviewPoint> Points,
    Money? FirstValue,
    Money? LastValue,
    Money? Change,
    decimal? ChangePercent,
    Money? ReferenceValue);
