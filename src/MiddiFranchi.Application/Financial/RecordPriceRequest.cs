namespace MiddiFranchi.Application.Financial;

public sealed record RecordPriceRequest(
    Guid InstrumentId,
    decimal Price,
    DateOnly ValidAt,
    string? Source = null);
