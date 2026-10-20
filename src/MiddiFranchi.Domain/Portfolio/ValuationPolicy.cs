namespace MiddiFranchi.Domain.Portfolio;

/// <summary>
/// Soglie di obsolescenza per prezzi e cambi. L'età è espressa in giorni di calendario
/// tra la data economica dell'osservazione (<c>ValidAt</c>) e la data di valutazione.
/// Un valore null significa nessun limite. Il Domain non fissa soglie predefinite:
/// i valori concreti restano una decisione applicativa.
/// </summary>
public sealed record ValuationPolicy
{
    public static ValuationPolicy Unbounded { get; } = new(null, null);

    public ValuationPolicy(int? maximumPriceAgeDays = null, int? maximumExchangeRateAgeDays = null)
    {
        if (maximumPriceAgeDays < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPriceAgeDays),
                "La soglia di obsolescenza dei prezzi non può essere negativa.");
        }

        if (maximumExchangeRateAgeDays < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumExchangeRateAgeDays),
                "La soglia di obsolescenza dei cambi non può essere negativa.");
        }

        MaximumPriceAgeDays = maximumPriceAgeDays;
        MaximumExchangeRateAgeDays = maximumExchangeRateAgeDays;
    }

    public int? MaximumPriceAgeDays { get; }

    public int? MaximumExchangeRateAgeDays { get; }

    public bool IsPriceStale(PriceObservation observation, DateOnly valuationDate) =>
        IsStale(observation.ValidAt, valuationDate, MaximumPriceAgeDays);

    public bool IsExchangeRateStale(ExchangeRateObservation observation, DateOnly valuationDate) =>
        IsStale(observation.ValidAt, valuationDate, MaximumExchangeRateAgeDays);

    private static bool IsStale(DateOnly validAt, DateOnly valuationDate, int? maximumAgeDays) =>
        maximumAgeDays is not null && valuationDate.DayNumber - validAt.DayNumber > maximumAgeDays.Value;
}
