using MiddiFranchi.Domain.Portfolio;

namespace MiddiFranchi.Application.Persistence;

public interface IInstrumentRepository
{
    Task<Instrument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Instrument>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Instrument instrument, CancellationToken cancellationToken = default);
}
