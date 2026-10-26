using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Modifica un'entrata o un'uscita esistente: categoria, importo, data, descrizione e,
/// quando richiesto, lo strumento finanziario associato. Il tipo dell'operazione non cambia
/// e il conto resta assente.
/// </summary>
public sealed class OperationUpdateService
{
    private readonly IFinancialOperationRepository _operations;
    private readonly ICategoryRepository _categories;
    private readonly IInstrumentRepository _instruments;

    public OperationUpdateService(
        IFinancialOperationRepository operations,
        ICategoryRepository categories,
        IInstrumentRepository instruments)
    {
        _operations = operations;
        _categories = categories;
        _instruments = instruments;
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

        Instrument? requestedInstrument = null;
        if (request.UpdateInstrument && request.InstrumentId is { } instrumentId)
        {
            requestedInstrument = await _instruments.GetByIdAsync(instrumentId, cancellationToken)
                ?? throw new InvalidOperationException("Strumento non trovato.");
        }

        FinancialOperation updated = operation switch
        {
            Income income => new Income(
                operation.Id,
                account: null,
                category,
                request.Amount,
                request.OperationDate,
                operation.RecordedAt,
                request.Description,
                notes: null,
                instrument: request.UpdateInstrument ? requestedInstrument : income.Instrument),
            Expense expense => new Expense(
                operation.Id,
                account: null,
                category,
                request.Amount,
                request.OperationDate,
                operation.RecordedAt,
                request.Description,
                notes: null,
                instrument: request.UpdateInstrument ? requestedInstrument : expense.Instrument),
            _ => throw new InvalidOperationException("Operazione non modificabile."),
        };

        await _operations.UpdateAsync(updated, cancellationToken);
    }
}
