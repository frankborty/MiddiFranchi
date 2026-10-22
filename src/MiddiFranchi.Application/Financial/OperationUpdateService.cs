using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Modifica un'entrata o un'uscita esistente: categoria, importo, data e descrizione.
/// Il tipo dell'operazione non cambia e il conto resta assente.
/// </summary>
public sealed class OperationUpdateService
{
    private readonly IFinancialOperationRepository _operations;
    private readonly ICategoryRepository _categories;

    public OperationUpdateService(
        IFinancialOperationRepository operations,
        ICategoryRepository categories)
    {
        _operations = operations;
        _categories = categories;
    }

    public async Task UpdateAsync(UpdateOperationRequest request, CancellationToken cancellationToken = default)
    {
        var operation = await _operations.GetByIdAsync(request.OperationId, cancellationToken)
            ?? throw new InvalidOperationException("Operazione non trovata.");

        if (operation.Kind is not (OperationKind.Income or OperationKind.Expense) ||
            operation.Status != OperationStatus.Registered)
        {
            throw new InvalidOperationException("Solo le entrate e le uscite attive possono essere modificate.");
        }

        var category = await _categories.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new InvalidOperationException("Categoria non trovata.");

        var expectedType = operation.Kind == OperationKind.Income
            ? CategoryType.Income
            : CategoryType.Expense;
        if (category.Type != expectedType)
        {
            throw new InvalidOperationException(
                $"La categoria {category.Name} non è compatibile con il tipo dell'operazione.");
        }

        FinancialOperation updated = operation switch
        {
            Income => new Income(
                operation.Id,
                account: null,
                category,
                request.Amount,
                request.OperationDate,
                operation.RecordedAt,
                request.Description),
            Expense => new Expense(
                operation.Id,
                account: null,
                category,
                request.Amount,
                request.OperationDate,
                operation.RecordedAt,
                request.Description),
            _ => throw new InvalidOperationException("Operazione non modificabile."),
        };

        await _operations.UpdateAsync(updated, cancellationToken);
    }
}
