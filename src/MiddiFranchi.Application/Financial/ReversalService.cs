using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Use case di storno di un'operazione già registrata. La costruzione dello storno e le
/// invarianti (operazione originale registrata, niente doppi storni, niente storno di
/// storno) sono nel Domain; la persistenza con le sue guardie è nel repository.
/// Lo storno non genera posting e non tocca le rilevazioni manuali.
/// </summary>
public sealed class ReversalService
{
    private readonly IFinancialOperationRepository _operations;

    public ReversalService(IFinancialOperationRepository operations)
    {
        _operations = operations;
    }

    public async Task<ReversalOperation> ReverseAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var original = await _operations.GetByIdAsync(operationId, cancellationToken)
            ?? throw new InvalidOperationException($"L'operazione {operationId} non esiste.");

        var reversal = new ReversalOperation(
            Guid.NewGuid(),
            original,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateTimeOffset.UtcNow);
        await _operations.AddAsync(reversal, cancellationToken);
        return reversal;
    }
}
