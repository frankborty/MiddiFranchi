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
    string? CategoryColor,
    Guid? InstrumentId = null,
    string? InstrumentName = null,
    bool IsInvestment = false)
{
    public bool IsReversed => Status == OperationStatus.Reversed;

    public bool IsReversal => Kind == OperationKind.Reversal;

    public bool IsReversible => !IsInvestment &&
        Kind is OperationKind.Income or OperationKind.Expense or OperationKind.Transfer &&
        Status == OperationStatus.Registered;

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
    /// Cronologia delle operazioni ordinarie (entrate, uscite, trasferimenti, storni) e delle
    /// cedole registrate dagli strumenti. Le altre operazioni d'investimento restano fuori.
    /// Ordine deterministico del repository (OperationDate, RecordedAt, Id).
    /// </summary>
    public async Task<IReadOnlyList<OperationHistoryRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        var operations = await _operations.ListAsync(cancellationToken);
        return operations
            .Where(operation => operation.Kind is not OperationKind.Investment ||
                operation is InvestmentOperation investment &&
                investment.InvestmentKind is
                    InvestmentOperationKind.Coupon or
                    InvestmentOperationKind.Dividend or
                    InvestmentOperationKind.Sell)
            .SelectMany(operation => operation is InvestmentOperation investment &&
                investment.InvestmentKind is
                    InvestmentOperationKind.Coupon or
                    InvestmentOperationKind.Dividend or
                    InvestmentOperationKind.Sell
                    ? ToInvestmentRows(investment)
                    : [ToOrdinaryRow(operation)])
            .ToArray();
    }

    private static IReadOnlyList<OperationHistoryRow> ToInvestmentRows(InvestmentOperation operation)
    {
        var rows = new List<OperationHistoryRow>();
        if (operation.InvestmentKind is
            InvestmentOperationKind.Coupon or InvestmentOperationKind.Dividend)
        {
            rows.Add(ToInvestmentIncomeRow(operation));
        }

        var tax = operation.Fees
            .Where(fee => fee.Kind == FeeKind.TaxWithheld)
            .Sum(fee => fee.Amount.Value.Amount);

        if (tax > 0m)
        {
            rows.Add(ToInvestmentTaxRow(operation, tax));
        }

        return rows;
    }

    private static OperationHistoryRow ToOrdinaryRow(FinancialOperation operation) => new(
        operation.Id,
        operation.Kind,
        operation.OperationDate,
        operation.RecordedAt,
        NormalizeDescription(operation.Description),
        operation.Status,
        operation.OriginalOperationId,
        AmountFor(operation),
        AccountLabelFor(operation),
        CategoryIdFor(operation),
        CategoryNameFor(operation),
        SubcategoryFor(operation),
        CategoryIconFor(operation),
        CategoryColorFor(operation),
        InstrumentIdFor(operation),
        InstrumentNameFor(operation));

    private static OperationHistoryRow ToInvestmentIncomeRow(InvestmentOperation operation) => new(
        operation.Id,
        OperationKind.Income,
        operation.OperationDate,
        operation.RecordedAt,
        NormalizeDescription(operation.Description) ?? "Cedola",
        operation.Status,
        OriginalOperationId: null,
        operation.Amount?.Value,
        AccountLabel: null,
        CategoryId: null,
        CategoryName: "Cedola",
        Subcategory: null,
        CategoryIcon: "percent",
        CategoryColor: "#0ea5e9",
        InstrumentId: operation.Instrument?.Id,
        InstrumentName: operation.Instrument?.Name,
        IsInvestment: true);

    private static OperationHistoryRow ToInvestmentTaxRow(InvestmentOperation operation, decimal tax) => new(
        operation.Id,
        OperationKind.Expense,
        operation.OperationDate,
        operation.RecordedAt,
        operation.InvestmentKind == InvestmentOperationKind.Sell
            ? "Tasse su plusvalenze"
            : "Tasse su cedola",
        operation.Status,
        OriginalOperationId: null,
        new Money(-tax, Currency.Euro),
        AccountLabel: null,
        CategoryId: null,
        CategoryName: "Fees",
        Subcategory: "Tasse Plusvalenze",
        CategoryIcon: "percent",
        CategoryColor: "#b4232c",
        InstrumentId: operation.Instrument?.Id,
        InstrumentName: operation.Instrument?.Name,
        IsInvestment: true);

    private static string? NormalizeDescription(string? description) =>
        description?
            .Replace("Cedola/Dividendo", "Cedola", StringComparison.CurrentCultureIgnoreCase)
            .Replace("Cedola / Dividendo", "Cedola", StringComparison.CurrentCultureIgnoreCase)
            .Trim();

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

    private static Guid? InstrumentIdFor(FinancialOperation operation) =>
        operation is Income income ? income.Instrument?.Id : null;

    private static string? InstrumentNameFor(FinancialOperation operation) =>
        operation is Income income ? income.Instrument?.Name : null;

}
