namespace MiddiFranchi.Application.Financial;

public sealed record PriceUpdateResult(
    Guid InstrumentId,
    string Name,
    bool Success,
    decimal? Price,
    DateOnly? PriceDate,
    string? Error)
{
    public static PriceUpdateResult Failure(Guid instrumentId, string name, string error) =>
        new(instrumentId, name, false, null, null, error);
}

public sealed record PriceUpdateStatus(
    DateTimeOffset? LastRunAt,
    DateTimeOffset? LastSuccessAt,
    bool IsRunning,
    string? LastError,
    IReadOnlyList<PriceUpdateResult> Results)
{
    public static PriceUpdateStatus Empty { get; } = new(
        LastRunAt: null,
        LastSuccessAt: null,
        IsRunning: false,
        LastError: null,
        Results: []);
}
