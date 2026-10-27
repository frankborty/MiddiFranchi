namespace MiddiFranchi.Web.Components.Shared;

public enum TimeSeriesSeriesKind
{
    Line,
    Bar,
    Area,
}

public sealed record TimeSeriesPoint(DateOnly Date, decimal? Value);

public sealed record TimeSeriesSeries(
    string Label,
    IReadOnlyList<TimeSeriesPoint> Points,
    string? Color = null,
    TimeSeriesSeriesKind Kind = TimeSeriesSeriesKind.Line,
    string? FillColor = null,
    double FillOpacity = 1);

public sealed record TimeSeriesPointAnnotation(
    DateOnly Date,
    decimal Value,
    string Color,
    string? Label = null,
    string? Tooltip = null);

public sealed record TimeSeriesRangePoint(DateOnly Date, decimal Top, decimal Bottom);

public sealed record TimeSeriesRangeSeries(
    string Label,
    IReadOnlyList<TimeSeriesRangePoint> Points,
    string Color,
    double FillOpacity = 0.2);
