using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Infrastructure.Persistence.Repositories;

public sealed class AccountPostingRepository : IAccountPostingRepository
{
    private readonly MiddiFranchiDbContext _context;

    public AccountPostingRepository(MiddiFranchiDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AccountPosting>> ListForAccountAsync(
        Guid accountId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var postingRecords = await _context.AccountPostings
            .AsNoTracking()
            .Where(posting =>
                posting.AccountId == accountId &&
                posting.Operation.OperationDate <= asOfDate &&
                !_context.FinancialOperations.Any(reversal =>
                    reversal.Kind == OperationKind.Reversal &&
                    reversal.OriginalOperationId == posting.FinancialOperationId))
            .OrderBy(posting => posting.Operation.OperationDate)
            .ThenBy(posting => posting.FinancialOperationId)
            .ThenBy(posting => posting.Id)
            .ToListAsync(cancellationToken);

        if (postingRecords.Count == 0)
        {
            return [];
        }

        var operationIds = postingRecords
            .Select(posting => posting.FinancialOperationId)
            .Distinct()
            .ToArray();
        var operationRecords = await FinancialOperationMaterializer.LoadByIdsAsync(
            _context, operationIds, cancellationToken);
        var operations = await FinancialOperationMaterializer.MaterializeAsync(
            _context, operationRecords, cancellationToken);
        var operationsById = operations.ToDictionary(operation => operation.Id);

        return postingRecords
            .Select(posting =>
            {
                if (!operationsById.TryGetValue(posting.FinancialOperationId, out var operation))
                {
                    throw new InvalidOperationException(
                        $"L'operazione {posting.FinancialOperationId} del posting {posting.Id} non è stata caricata.");
                }

                return new AccountPosting(
                    operation,
                    posting.AccountId,
                    posting.Kind,
                    Currency.Parse(posting.CurrencyCode),
                    posting.SignedAmount);
            })
            .ToArray();
    }
}
