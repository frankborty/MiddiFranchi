using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Persistence;

public interface IExchangeRateObservationRepository
{
    Task<IReadOnlyList<ExchangeRateObservation>> ListForCurrencyAsync(
        Currency currency,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);

    Task AddAsync(ExchangeRateObservation observation, CancellationToken cancellationToken = default);
}
