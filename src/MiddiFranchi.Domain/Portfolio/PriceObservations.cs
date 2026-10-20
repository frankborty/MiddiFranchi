namespace MiddiFranchi.Domain.Portfolio;

public static class PriceObservations
{
    public static PriceObservation? MostRecentOnOrBefore(
        IEnumerable<PriceObservation> observations,
        Instrument instrument,
        DateOnly asOfDate)
    {
        if (observations is null)
        {
            throw new ArgumentNullException(nameof(observations));
        }

        if (instrument is null)
        {
            throw new ArgumentNullException(nameof(instrument));
        }

        return observations
            .Where(observation => observation.Instrument.Id == instrument.Id)
            .Where(observation => observation.ValidAt <= asOfDate)
            .OrderByDescending(observation => observation.ValidAt)
            .ThenByDescending(observation => observation.ObservedAt)
            .ThenByDescending(observation => observation.Source, StringComparer.Ordinal)
            .ThenByDescending(observation => observation.Id)
            .FirstOrDefault();
    }
}
