namespace MiddiFranchi.Web.Components.Shared;

public sealed record TimeSeriesPoint(DateOnly Date, decimal? Value);

public sealed record TimeSeriesSeries(
    string Label,
    IReadOnlyList<TimeSeriesPoint> Points,
    string? Color = null);
