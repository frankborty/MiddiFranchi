using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Persistence;

public interface IManualBalanceRepository
{
    Task AddAsync(ManualBalance reading, CancellationToken cancellationToken = default);

    Task<ManualBalance?> GetLatestAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<ManualBalance?> GetLatestOnOrBeforeAsync(
        Guid accountId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManualBalance>> ListForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManualBalance>> ListAsync(CancellationToken cancellationToken = default);
}
