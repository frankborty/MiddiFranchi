using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Financial;

public sealed record OperationHistoryRow(
    Guid Id,
    OperationKind Kind,
    DateOnly OperationDate,
    DateTimeOffset RecordedAt,
    string? Description,
    OperationStatus Status,
    Guid? OriginalOperationId)
{
    public bool IsReversed => Status == OperationStatus.Reversed;

    public bool IsReversal => Kind == OperationKind.Reversal;

    public bool IsReversible => Kind is OperationKind.Income or OperationKind.Expense or OperationKind.Transfer
        && Status == OperationStatus.Registered;
}

public sealed class OperationHistoryService
{
    private readonly IFinancialOperationRepository _operations;

    public OperationHistoryService(IFinancialOperationRepository operations)
    {
        _operations = operations;
    }

    /// <summary>
    /// Cronologia delle operazioni ordinarie (entrate, uscite, trasferimenti, storni):
    /// le operazioni d'investimento restano fuori da questo incremento. Ordine
    /// deterministico del repository (OperationDate, RecordedAt, Id).
    /// </summary>
    public async Task<IReadOnlyList<OperationHistoryRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        var operations = await _operations.ListAsync(cancellationToken);
        return operations
            .Where(operation => operation.Kind is not OperationKind.Investment)
            .Select(operation => new OperationHistoryRow(
                operation.Id,
                operation.Kind,
                operation.OperationDate,
                operation.RecordedAt,
                operation.Description,
                operation.Status,
                operation.OriginalOperationId))
            .ToArray();
    }
}
