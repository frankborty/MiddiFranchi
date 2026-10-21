using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Financial;

public sealed class MovementService
{
    private readonly IAccountPostingRepository _postings;

    public MovementService(IAccountPostingRepository postings)
    {
        _postings = postings;
    }

    public Task<IReadOnlyList<AccountPosting>> ListAsync(
        Guid? accountId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default) =>
        _postings.ListAsync(accountId, fromDate, toDate, cancellationToken);
}
