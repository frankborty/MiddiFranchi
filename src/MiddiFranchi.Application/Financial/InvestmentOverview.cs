using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Riepilogo degli investimenti. Tutti i valori, compresi portafogli e asset, sono in EUR.
/// </summary>
public sealed record InvestmentOverview(
    DateOnly ValuationDate,
    Money? InvestedCapital,
    Money? CurrentValue,
    Money? Performance,
    decimal? PerformancePercent,
    bool IsComplete,
    IReadOnlyList<string> IncompletenessReasons,
    IReadOnlyList<InvestmentPortfolioOverview> Portfolios,
    InvestmentHistory History)
{
    public bool HasPortfolios => Portfolios.Count > 0;

    public int AssetCount => Portfolios.Sum(portfolio => portfolio.Assets.Count);

    public bool HasAssets => AssetCount > 0;
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

public sealed record InvestmentPortfolioOverview(
    Guid PortfolioId,
    string Name,
    Currency ReferenceCurrency,
    Money? InvestedCapital,
    Money? CurrentValue,
    Money? Performance,
    decimal? PerformancePercent,
    bool IsComplete,
    IReadOnlyList<string> IncompletenessReasons,
    IReadOnlyList<InvestmentAssetOverview> Assets)
{
    public bool HasAssets => Assets.Count > 0;

    public int ValuedAssetCount => Assets.Count(asset => asset.CurrentValue is not null);

    public int UnvaluedAssetCount => Assets.Count(asset => asset.CurrentValue is null);
}

public sealed record InvestmentAssetOverview(
    Guid InstrumentId,
    string Name,
    string? Symbol,
    InstrumentType Type,
    Quantity Quantity,
    Money? InvestedCapital,
    Money? CurrentValue,
    Money? Performance,
    decimal? PerformancePercent,
    bool IsComplete,
    string? IncompletenessReason);
