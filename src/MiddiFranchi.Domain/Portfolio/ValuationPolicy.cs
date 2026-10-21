namespace MiddiFranchi.Domain.Portfolio;

/// <summary>
/// Soglia di obsolescenza per le quotazioni. L'età è espressa in giorni di calendario
/// tra la data economica dell'osservazione (<c>ValidAt</c>) e la data di valutazione.
/// Un valore null significa nessun limite. Il Domain non fissa soglie predefinite.
/// </summary>
public sealed record ValuationPolicy
{
    public static ValuationPolicy Unbounded { get; } = new ValuationPolicy((int?)null);

    public ValuationPolicy(int? maximumPriceAgeDays = null)
    {
        if (maximumPriceAgeDays < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPriceAgeDays),
                "La soglia di obsolescenza dei prezzi non può essere negativa.");
        }

        MaximumPriceAgeDays = maximumPriceAgeDays;
    }

    public int? MaximumPriceAgeDays { get; }

    public bool IsPriceStale(PriceObservation observation, DateOnly valuationDate) =>
        MaximumPriceAgeDays is not null &&
        valuationDate.DayNumber - observation.ValidAt.DayNumber > MaximumPriceAgeDays.Value;
}
