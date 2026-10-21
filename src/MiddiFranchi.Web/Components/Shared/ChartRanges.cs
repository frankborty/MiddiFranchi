namespace MiddiFranchi.Web.Components.Shared;

/// <summary>Intervalli temporali condivisi dai grafici dei conti.</summary>
public static class ChartRanges
{
    public static readonly (string Key, string Label)[] Options =
    [
        ("year", "Anno"),
        ("1a", "1A"),
        ("3a", "3A"),
        ("max", "Max"),
    ];

    public static DateOnly? Start(string key, DateOnly today) => key switch
    {
        "year" => new DateOnly(today.Year, 1, 1),
        "1a" => new DateOnly(today.Year, today.Month, 1).AddMonths(-11),
        "3a" => new DateOnly(today.Year, today.Month, 1).AddMonths(-35),
        _ => null,
    };

    public static string Class(string key, string selected) =>
        string.Equals(key, selected, StringComparison.Ordinal)
            ? "overview-range active"
            : "overview-range";
}
