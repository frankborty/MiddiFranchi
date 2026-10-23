using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Riepilogo degli investimenti. Tutti i valori, compresi gli asset, sono in EUR.
/// Il rendimento non realizzato è la differenza tra valore attuale e costo residuo; il risultato
/// totale aggiunge plusvalenze realizzate e proventi e sottrae le commissioni autonome.
/// </summary>
public sealed record InvestmentOverview(
    DateOnly ValuationDate,
    Money? InvestedCapital,
    Money? CurrentValue,
    Money? Performance,
    decimal? PerformancePercent,
    Money? RealizedResult,
    Money? Income,
    Money? OtherFees,
    Money? TotalResult,
    decimal? TotalResultPercent,
    bool IsComplete,
    IReadOnlyList<string> IncompletenessReasons,
    IReadOnlyList<InvestmentAssetOverview> Assets,
    InvestmentHistory History)
{
    public int AssetCount => Assets.Count;

    public bool HasAssets => Assets.Count > 0;
}

public sealed record InvestmentHistory(
    DateOnly From,
    DateOnly To,
    Currency Currency,
    IReadOnlyList<InvestmentHistoryPoint> Points,
    bool IsComplete,
    IReadOnlyList<string> IncompletenessReasons);

public sealed record InvestmentHistoryPoint(
    DateOnly Date,
    Money? InvestedCapital,
    Money? CurrentValue);

public sealed record InvestmentAssetOverview(
    Guid InstrumentId,
    string Name,
    string? Symbol,
    AssetClass AssetClass,
    Quantity Quantity,
    Money? InvestedCapital,
    Money? AverageCost,
    Money? CurrentValue,
    Money? UnrealizedResult,
    Money RealizedResult,
    Money Income,
    Money OtherFees,
    Money? TotalResult,
    decimal? TotalResultPercent,
    bool IsComplete,
    string? IncompletenessReason);
