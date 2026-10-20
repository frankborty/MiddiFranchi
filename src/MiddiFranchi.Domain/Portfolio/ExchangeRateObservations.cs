using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

public static class ExchangeRateObservations
{
    public static ExchangeRateObservation? MostRecentOnOrBefore(
        IEnumerable<ExchangeRateObservation> observations,
        Currency quotedCurrency,
        DateOnly asOfDate)
    {
        if (observations is null)
        {
            throw new ArgumentNullException(nameof(observations));
        }

        if (quotedCurrency.Code is null)
        {
            throw new ArgumentException("La valuta dell'osservazione di cambio è obbligatoria.", nameof(quotedCurrency));
        }

        return observations
            .Where(observation => observation.Rate.Currency.Code == quotedCurrency.Code)
            .Where(observation => observation.ValidAt <= asOfDate)
            .OrderByDescending(observation => observation.ValidAt)
            .ThenByDescending(observation => observation.ObservedAt)
            .ThenByDescending(observation => observation.Source, StringComparer.Ordinal)
            .ThenByDescending(observation => observation.Id)
            .FirstOrDefault();
    }
}
