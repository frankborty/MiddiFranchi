using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Modello di lettura della pagina Investimenti: riepilogo, storico, allocazione per classe di
/// attività e catalogo completo degli strumenti, compresi quelli senza posizione o archiviati.
/// </summary>
public sealed record InvestmentWorkspace(
    InvestmentOverview Overview,
    IReadOnlyList<InvestmentAssetRow> Assets,
    IReadOnlyList<InvestmentClassAllocation> Allocation,
    Money TotalIncome,
    Money NetIncome,
    int ArchivedCount,
    int CatalogCount,
    IReadOnlyList<InvestmentMonthlyReturn> MonthlyReturns);

/// <summary>
/// Rendimento di un mese calcolato con i flussi esterni: acquisti e vendite non sono performance,
/// mentre plusvalenze realizzate, cedole, dividendi e costi sì.
/// </summary>
public sealed record InvestmentMonthlyReturn(DateOnly Month, decimal? Return);

public sealed record InvestmentAssetRow(
    Guid InstrumentId,
    string Name,
    AssetClass AssetClass,
    InstrumentStatus Status,
    string? Symbol,
    string? Isin,
    string? ReferenceUrl,
    string? Notes,
    Currency QuoteCurrency,
    PriceUnit PriceUnit,
    Money? NotionalValue,
    DateOnly? MaturityDate,
    decimal? CouponRate,
    decimal TaxRate,
    Quantity? Quantity,
    Money? AverageCost,
    decimal? AverageCostPercentOfNominal,
    Money? InvestedCapital,
    Money? CurrentValue,
    Money? UnrealizedResult,
    Money RealizedResult,
    Money Income,
    Money OtherFees,
    Money? TotalResult,
    decimal? TotalResultPercent,
    Money? CurrentUnitPrice,
    decimal? LatestPrice,
    DateOnly? PriceDate,
    string? PriceSource,
    bool IsPriceStale,
    bool HasOperations,
    IReadOnlyList<InvestmentOperationRow> Operations,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<InvestmentHistoryPoint>? History = null,
    IReadOnlyList<InvestmentPricePoint>? PriceHistory = null)
{
    public bool IsOpen => Quantity is { Value: > 0m };

    public bool IsArchived => Status == InstrumentStatus.Archived;

    public bool IsValuationComplete => Quantity is null || CurrentValue is not null;
}

public sealed record InvestmentPricePoint(DateOnly Date, decimal Price, Currency Currency);

public sealed record InvestmentOperationRow(
    Guid Id,
    Guid SettlementAccountId,
    InvestmentOperationKind Kind,
    DateOnly OperationDate,
    Quantity? Quantity,
    decimal? Price,
    PriceUnit? PriceUnit,
    Money? EffectiveUnitPrice,
    Money? GrossAmount,
    Money? Fees,
    Money? Amount,
    string? Description,
    string SettlementAccountName,
    bool IsReversed,
    Money? Tax = null,
    DateTimeOffset RecordedAt = default);

public sealed record InvestmentClassAllocation(
    AssetClass AssetClass,
    Money? Value,
    decimal? Share,
    int AssetCount);

public sealed record InvestmentOption(Guid Id, string Name);
