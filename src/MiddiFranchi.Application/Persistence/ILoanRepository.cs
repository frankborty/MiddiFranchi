using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Persistence;

public interface ILoanRepository
{
    Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Loan>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Loan>> ListForAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task AddAsync(Loan loan, CancellationToken cancellationToken = default);

    /// <summary>Elimina il prestito e tutte le sue rilevazioni.</summary>
    Task DeleteAsync(Guid loanId, CancellationToken cancellationToken = default);
}
