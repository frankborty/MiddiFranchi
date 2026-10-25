using MiddiFranchi.Domain.Portfolio;

namespace MiddiFranchi.Application.Persistence;

public interface IPriceObservationRepository
{
    Task<IReadOnlyList<PriceObservation>> ListForInstrumentAsync(
        Guid instrumentId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PriceObservation>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> AnyForInstrumentAsync(Guid instrumentId, CancellationToken cancellationToken = default);

    Task AddAsync(PriceObservation observation, CancellationToken cancellationToken = default);

    Task UpdateAsync(PriceObservation observation, CancellationToken cancellationToken = default);
}
