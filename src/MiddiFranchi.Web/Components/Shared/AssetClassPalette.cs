using MiddiFranchi.Domain.Portfolio;

namespace MiddiFranchi.Web.Components.Shared;

/// <summary>Colori e toni coerenti per le classi di attività, usati da tabella e grafici.</summary>
public static class AssetClassPalette
{
    private static readonly IReadOnlyDictionary<AssetClass, string> Colors =
        new Dictionary<AssetClass, string>
        {
            [AssetClass.Equity] = "#8b5cf6",
            [AssetClass.Bond] = "#0c8fa5",
            [AssetClass.MoneyMarket] = "#22c55e",
            [AssetClass.RealEstate] = "#2456c9",
            [AssetClass.Commodity] = "#f59e0b",
            [AssetClass.Crypto] = "#b53a4a",
            [AssetClass.Mixed] = "#6366f1",
            [AssetClass.Pension] = "#0ea5e9",
            [AssetClass.Other] = "#64748b",
        };

    private static readonly IReadOnlyDictionary<AssetClass, string> Tones =
        new Dictionary<AssetClass, string>
        {
            [AssetClass.Equity] = "investments",
            [AssetClass.Bond] = "teal",
            [AssetClass.MoneyMarket] = "income",
            [AssetClass.RealEstate] = "brand",
            [AssetClass.Commodity] = "amber",
            [AssetClass.Crypto] = "expense",
            [AssetClass.Mixed] = "info",
            [AssetClass.Pension] = "accounts",
            [AssetClass.Other] = "neutral",
        };

    public static string Color(AssetClass assetClass) =>
        Colors.TryGetValue(assetClass, out var color) ? color : "#64748b";

    public static string Tone(AssetClass assetClass) =>
        Tones.TryGetValue(assetClass, out var tone) ? tone : "neutral";
}
