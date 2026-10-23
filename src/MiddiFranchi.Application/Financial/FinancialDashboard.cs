using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Vista della dashboard costruita esclusivamente dai servizi applicativi esistenti.
/// I valori assenti restano assenti: nessun campo viene sostituito con zero.
/// </summary>
public sealed record FinancialDashboard(
    NetWorthSummary NetWorth,
    IReadOnlyList<AccountOverviewRow> Accounts,
    DashboardMonthSummary Month,
    IReadOnlyList<DashboardMovement> RecentMovements,
    DashboardInvestments Investments);

/// <summary>
/// Riepilogo del mese corrente. I totali riguardano solo le operazioni ordinarie di entrata
/// e uscita; trasferimenti e operazioni d'investimento sono conteggiati a parte e non entrano
/// nei totali economici. Le valute restano separate.
/// </summary>
public sealed record DashboardMonthSummary(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<DashboardCurrencyFlow> Flows,
    int MovementCount,
    int IncomeExpenseOperationCount,
    int OtherOperationCount)
{
    public bool HasIncomeExpense => Flows.Count > 0;

    public bool HasMovements => MovementCount > 0;

    public bool HasOtherOperations => OtherOperationCount > 0;
}

public sealed record DashboardCurrencyFlow(
    Currency Currency,
    decimal Income,
    decimal Expense,
    int OperationCount)
{
    public decimal Net => Income - Expense;
}

/// <summary>
/// Ultima operazione registrata, una riga per operazione. Per i trasferimenti
/// <see cref="CounterAmount"/> riporta l'importo accreditato sul conto di destinazione.
/// Categoria, sottocategoria, icona e colore descrivono il movimento senza ripetere il tipo.
/// </summary>
public sealed record DashboardMovement(
    Guid OperationId,
    DateOnly Date,
    OperationKind OperationKind,
    string? Description,
    string? Category,
    string? Subcategory,
    string? CategoryIcon,
    string? CategoryColor,
    string? AccountLabel,
    Money? Amount,
    Money? CounterAmount)
{
    public string? CategoryLabel => string.IsNullOrWhiteSpace(Category)
        ? null
        : string.IsNullOrWhiteSpace(Subcategory)
            ? Category
            : $"{Category} · {Subcategory}";
}

/// <summary>
/// Valore complessivo degli investimenti solo quando il servizio del patrimonio lo rende
/// disponibile in EUR; in caso contrario <see cref="Total"/> è null e restano i conteggi
/// e le posizioni.
/// </summary>
public sealed record DashboardInvestments(
    Money? Total,
    int ValuedPositionCount,
    int UnvaluedPositionCount,
    IReadOnlyList<DashboardInvestmentPosition> Positions)
{
    public bool HasPositions => Positions.Count > 0;

    public bool HasValuedPositions => ValuedPositionCount > 0;

    public bool HasUnvaluedPositions => UnvaluedPositionCount > 0;
}

public sealed record DashboardInvestmentPosition(
    string InstrumentName,
    string? Symbol,
    Money? Value,
    bool IsValuated,
    string? IncompletenessReason);
