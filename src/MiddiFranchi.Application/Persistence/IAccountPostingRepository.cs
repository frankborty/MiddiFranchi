using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Persistence;

public interface IAccountPostingRepository
{
    Task<IReadOnlyList<AccountPosting>> ListForAccountAsync(
        Guid accountId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountPosting>> ListAsync(
        Guid? accountId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default);
}
