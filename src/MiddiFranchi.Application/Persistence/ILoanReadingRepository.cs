using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Persistence;

public interface ILoanReadingRepository
{
    Task<LoanReading?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoanReading>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoanReading>> ListForLoanAsync(Guid loanId, CancellationToken cancellationToken = default);

    Task AddAsync(LoanReading reading, CancellationToken cancellationToken = default);

    Task UpdateAsync(LoanReading reading, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
