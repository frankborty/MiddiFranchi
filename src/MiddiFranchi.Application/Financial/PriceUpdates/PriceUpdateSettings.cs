namespace MiddiFranchi.Application.Financial;

public enum PriceUpdateFrequency
{
    Daily,
    Weekdays,
    Every12Hours,
    Every6Hours,
}

public sealed record PriceUpdateSettings(
    bool Enabled,
    TimeOnly TimeOfDay,
    PriceUpdateFrequency Frequency,
    string Exchange)
{
    public static PriceUpdateSettings Default { get; } = new(
        Enabled: true,
        TimeOfDay: new TimeOnly(12, 0),
        Frequency: PriceUpdateFrequency.Daily,
        Exchange: "XMIL");
}
