using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Riga della cronologia operativa con il dettaglio leggibile: importo con segno
/// (le uscite sono negative), conto coinvolto e categoria per entrate e uscite.
/// </summary>
public sealed record OperationHistoryRow(
    Guid Id,
    OperationKind Kind,
    DateOnly OperationDate,
    DateTimeOffset RecordedAt,
    string? Description,
    OperationStatus Status,
    Guid? OriginalOperationId,
    Money? Amount,
    string? AccountLabel,
    Guid? CategoryId,
    string? CategoryName,
    string? Subcategory,
    string? CategoryIcon,
    string? CategoryColor)
{
    public bool IsReversed => Status == OperationStatus.Reversed;

    public bool IsReversal => Kind == OperationKind.Reversal;

    public bool IsReversible => Kind is OperationKind.Income or OperationKind.Expense or OperationKind.Transfer
        && Status == OperationStatus.Registered;

    public string? CategoryLabel => string.IsNullOrWhiteSpace(CategoryName)
        ? null
        : string.IsNullOrWhiteSpace(Subcategory)
            ? CategoryName
            : $"{CategoryName} · {Subcategory}";
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
                operation.OriginalOperationId,
                AmountFor(operation),
                AccountLabelFor(operation),
                CategoryIdFor(operation),
                CategoryNameFor(operation),
                SubcategoryFor(operation),
                CategoryIconFor(operation),
                CategoryColorFor(operation)))
            .ToArray();
    }

    private static Money? AmountFor(FinancialOperation operation) => operation switch
    {
        Income income => income.Amount.Value,
        Expense expense => new Money(-expense.Amount.Value.Amount, expense.Amount.Value.Currency),
        Transfer transfer => transfer.SourceAmount.Value,
        _ => null,
    };

    private static string? AccountLabelFor(FinancialOperation operation) => operation switch
    {
        Income income => income.Account?.Name,
        Expense expense => expense.Account?.Name,
        Transfer transfer => $"{transfer.SourceAccount.Name} → {transfer.DestinationAccount.Name}",
        _ => null,
    };

    private static Guid? CategoryIdFor(FinancialOperation operation) => operation switch
    {
        Income income => income.Category.Id,
        Expense expense => expense.Category.Id,
        _ => null,
    };

    private static string? CategoryNameFor(FinancialOperation operation) => operation switch
    {
        Income income => income.Category.Name,
        Expense expense => expense.Category.Name,
        _ => null,
    };

    private static string? SubcategoryFor(FinancialOperation operation) => operation switch
    {
        Income income => income.Category.Subcategory,
        Expense expense => expense.Category.Subcategory,
        _ => null,
    };

    private static string? CategoryIconFor(FinancialOperation operation) => operation switch
    {
        Income income => income.Category.Icon,
        Expense expense => expense.Category.Icon,
        _ => null,
    };

    private static string? CategoryColorFor(FinancialOperation operation) => operation switch
    {
        Income income => income.Category.ColorHex,
        Expense expense => expense.Category.ColorHex,
        _ => null,
    };

}
