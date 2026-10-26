using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

public sealed record RegisterInvestmentIncomeRequest(
    Guid InstrumentId,
    Guid IncomeCategoryId,
    Guid TaxCategoryId,
    PositiveMoney GrossAmount,
    PositiveMoney? TaxAmount,
    DateOnly OperationDate,
    string? Description = null);

public sealed record InvestmentIncomeResult(Income Income, Expense? TaxExpense);

/// <summary>
/// Registra una cedola o un dividendo come entrata lorda associata allo strumento e,
/// quando presente, la tassa come uscita separata. Le due operazioni sono transazionali.
/// </summary>
public sealed class InvestmentIncomeService
{
    private readonly IFinancialOperationRepository _operations;
    private readonly ICategoryRepository _categories;
    private readonly IInstrumentRepository _instruments;
    private readonly ITransactionRunner _transactions;

    public InvestmentIncomeService(
        IFinancialOperationRepository operations,
        ICategoryRepository categories,
        IInstrumentRepository instruments,
        ITransactionRunner transactions)
    {
        _operations = operations;
        _categories = categories;
        _instruments = instruments;
        _transactions = transactions;
    }

    public async Task<InvestmentIncomeResult> RegisterAsync(
        RegisterInvestmentIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var instrument = await _instruments.GetByIdAsync(request.InstrumentId, cancellationToken)
            ?? throw new InvalidOperationException("Lo strumento selezionato non esiste.");
        var incomeCategory = await RequireCategoryAsync(
            request.IncomeCategoryId, CategoryType.Income, "un'entrata", cancellationToken);
        var taxCategory = await RequireCategoryAsync(
            request.TaxCategoryId, CategoryType.Expense, "un'uscita", cancellationToken);

        var label = string.IsNullOrWhiteSpace(request.Description)
            ? "Cedola"
            : request.Description.Trim();
        var recordedAt = DateTimeOffset.UtcNow;

        return await _transactions.RunAsync(async token =>
        {
            var incomeId = Guid.NewGuid();
            var income = new Income(
                incomeId,
                account: null,
                incomeCategory,
                request.GrossAmount,
                request.OperationDate,
                recordedAt,
                label,
                notes: null,
                instrument: instrument);
            await _operations.AddAsync(income, token);

            Expense? taxExpense = null;
            if (request.TaxAmount is { } tax && tax.Value.Amount > 0m)
            {
                taxExpense = new Expense(
                    InvestmentExpenses.TaxIdForOperation(incomeId),
                    account: null,
                    taxCategory,
                    tax,
                    request.OperationDate,
                    recordedAt,
                    $"Tasse su {instrument.Name}",
                    notes: null,
                    instrument: instrument);
                await _operations.AddAsync(taxExpense, token);
            }

            return new InvestmentIncomeResult(income, taxExpense);
        }, cancellationToken);
    }

    private async Task<Category> RequireCategoryAsync(
        Guid categoryId,
        CategoryType expectedType,
        string operationDescription,
        CancellationToken cancellationToken)
    {
        var category = await _categories.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new InvalidOperationException($"La categoria {categoryId} non esiste.");

        if (category.Type != expectedType)
        {
            throw new InvalidOperationException(
                $"La categoria {category.Name} non è di tipo {expectedType} e non può essere usata per {operationDescription}.");
        }

        return category;
    }
}
