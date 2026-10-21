using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Persistence;

public interface IManualBalanceRepository
{
    Task AddAsync(ManualBalance reading, CancellationToken cancellationToken = default);

    Task<ManualBalance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ManualBalance?> GetLatestAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<ManualBalance?> GetLatestOnOrBeforeAsync(
        Guid accountId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManualBalance>> ListForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManualBalance>> ListAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(ManualBalance reading, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sostituisce in blocco le rilevazioni del conto, in transazione. Usato per materializzare
    /// lo storico aggregato del conto Prestiti dalle rilevazioni dei singoli prestiti.
    /// </summary>
    Task ReplaceForAccountAsync(
        Guid accountId,
        IReadOnlyList<ManualBalance> readings,
        CancellationToken cancellationToken = default);
}
