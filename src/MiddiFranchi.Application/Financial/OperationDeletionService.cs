using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Elimina definitivamente un'operazione ordinaria e i suoi effetti.
/// A differenza dello storno, non resta alcuna traccia nel database.
/// </summary>
public sealed class OperationDeletionService
{
    private readonly IFinancialOperationRepository _operations;

    public OperationDeletionService(IFinancialOperationRepository operations)
    {
        _operations = operations;
    }

    public async Task DeleteAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var operation = await _operations.GetByIdAsync(operationId, cancellationToken)
            ?? throw new InvalidOperationException("Operazione non trovata.");

        if (operation.Kind is not (OperationKind.Income or OperationKind.Expense))
        {
            throw new InvalidOperationException("Solo le entrate e le uscite possono essere eliminate da questa pagina.");
        }

        // Una cedola registrata come entrata ha la ritenuta in una spesa separata.
        if (operation is Income)
        {
            var taxExpenseId = InvestmentExpenses.TaxIdForOperation(operation.Id);
            if (await _operations.GetByIdAsync(taxExpenseId, cancellationToken) is not null)
            {
                await _operations.DeleteAsync(taxExpenseId, cancellationToken);
            }
        }

        await _operations.DeleteAsync(operationId, cancellationToken);
    }
}
