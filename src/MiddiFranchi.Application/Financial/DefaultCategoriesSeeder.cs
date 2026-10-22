using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Financial;

public sealed record DefaultCategory(
    string Name,
    string? Subcategory,
    CategoryType Type,
    string? Icon = null,
    string? ColorHex = null);

/// <summary>
/// Inserisce le categorie di partenza per entrate e uscite. L'operazione è idempotente:
/// confronta nome, sottocategoria e tipo e non tocca categorie già presenti o personalizzate.
/// </summary>
public sealed class DefaultCategoriesSeeder
{
    public static readonly IReadOnlyList<DefaultCategory> Defaults =
    [
        // Entrate: nessuna sottocategoria, icona e colore per categoria.
        new("Stipendio", null, CategoryType.Income, "wallet", "#16a34a"),
        new("Regali", null, CategoryType.Income, "gift", "#ec4899"),
        new("ExtraStipendio", null, CategoryType.Income, "cash", "#14b8a6"),
        new("Patto", null, CategoryType.Income, "doc", "#2563eb"),
        new("Concorrenza", null, CategoryType.Income, "briefcase", "#8b5cf6"),
        new("Premio Produzione", null, CategoryType.Income, "trophy", "#f59e0b"),
        new("Rimborsi", null, CategoryType.Income, "receipt", "#64748b"),
        new("AltroIncome", null, CategoryType.Income, "tag", "#94a3b8"),

        // Uscite con icona e colore univoco per categoria.
        new("Abitazione & Utenze", "Elettricità Maranello", CategoryType.Expense, "home", "#2563eb"),
        new("Abitazione & Utenze", "Elettricità NonnaMaria", CategoryType.Expense, "home", "#2563eb"),
        new("Abitazione & Utenze", "Elettricità Mamma", CategoryType.Expense, "home", "#2563eb"),
        new("Abitazione & Utenze", "Gas", CategoryType.Expense, "home", "#2563eb"),
        new("Abitazione & Utenze", "Telefono", CategoryType.Expense, "home", "#2563eb"),
        new("Abitazione & Utenze", "Internet", CategoryType.Expense, "home", "#2563eb"),
        new("Abitazione & Utenze", "AltroCasa", CategoryType.Expense, "home", "#2563eb"),

        new("Trasporti", "Mezzi pubblici", CategoryType.Expense, "bus", "#0ea5e9"),
        new("Trasporti", "Pedaggio", CategoryType.Expense, "bus", "#0ea5e9"),
        new("Trasporti", "Nave", CategoryType.Expense, "bus", "#0ea5e9"),
        new("Trasporti", "Aereo", CategoryType.Expense, "bus", "#0ea5e9"),
        new("Trasporti", "AltroTrasporti", CategoryType.Expense, "bus", "#0ea5e9"),

        new("Auto", "Assicurazione", CategoryType.Expense, "car", "#f59e0b"),
        new("Auto", "Bollo", CategoryType.Expense, "car", "#f59e0b"),
        new("Auto", "Tagliando", CategoryType.Expense, "car", "#f59e0b"),
        new("Auto", "Revisione", CategoryType.Expense, "car", "#f59e0b"),
        new("Auto", "Penumatici", CategoryType.Expense, "car", "#f59e0b"),
        new("Auto", "Rifornimento", CategoryType.Expense, "car", "#f59e0b"),
        new("Auto", "Meccanico", CategoryType.Expense, "car", "#f59e0b"),
        new("Auto", "AltroAuto", CategoryType.Expense, "car", "#f59e0b"),

        new("Fees", "Postepay", CategoryType.Expense, "receipt", "#64748b"),
        new("Fees", "Conto Titoli", CategoryType.Expense, "receipt", "#64748b"),
        new("Fees", "Conto Corrente", CategoryType.Expense, "receipt", "#64748b"),
        new("Fees", "AltroFees", CategoryType.Expense, "receipt", "#64748b"),

        new("Fun & Shopping", "Vestiti", CategoryType.Expense, "shopping-bag", "#ec4899"),
        new("Fun & Shopping", "Tech", CategoryType.Expense, "shopping-bag", "#ec4899"),
        new("Fun & Shopping", "AltroShopping", CategoryType.Expense, "shopping-bag", "#ec4899"),
        new("Fun & Shopping", "Mare", CategoryType.Expense, "shopping-bag", "#ec4899"),
        new("Fun & Shopping", "Bar", CategoryType.Expense, "shopping-bag", "#ec4899"),
        new("Fun & Shopping", "Ristoranti", CategoryType.Expense, "shopping-bag", "#ec4899"),
        new("Fun & Shopping", "AltroFun", CategoryType.Expense, "shopping-bag", "#ec4899"),
        new("Fun & Shopping", "Viaggi", CategoryType.Expense, "shopping-bag", "#ec4899"),
        new("Fun & Shopping", "Feste", CategoryType.Expense, "shopping-bag", "#ec4899"),

        new("Salute & Benessere", "Medico", CategoryType.Expense, "heart-pulse", "#ef4444"),
        new("Salute & Benessere", "Occhiali & Lenti", CategoryType.Expense, "heart-pulse", "#ef4444"),
        new("Salute & Benessere", "Barbiere", CategoryType.Expense, "heart-pulse", "#ef4444"),
        new("Salute & Benessere", "Medicine", CategoryType.Expense, "heart-pulse", "#ef4444"),
        new("Salute & Benessere", "Palestra", CategoryType.Expense, "heart-pulse", "#ef4444"),
        new("Salute & Benessere", "AltroSalute", CategoryType.Expense, "heart-pulse", "#ef4444"),

        new("Alimentazione", "Spesa", CategoryType.Expense, "utensils", "#22c55e"),
        new("Alimentazione", "Delivery", CategoryType.Expense, "utensils", "#22c55e"),
        new("Alimentazione", "AltroSpesa", CategoryType.Expense, "utensils", "#22c55e"),

        new("Streaming", "Prime", CategoryType.Expense, "tv", "#8b5cf6"),
        new("Streaming", "Dazn", CategoryType.Expense, "tv", "#8b5cf6"),
        new("Streaming", "Now", CategoryType.Expense, "tv", "#8b5cf6"),
        new("Streaming", "HBO", CategoryType.Expense, "tv", "#8b5cf6"),
        new("Streaming", "TimVision", CategoryType.Expense, "tv", "#8b5cf6"),

        new("Varie", "Regali", CategoryType.Expense, "gift", "#14b8a6"),
        new("Varie", "Superenalotto", CategoryType.Expense, "gift", "#14b8a6"),
        new("Varie", "AltroVarie", CategoryType.Expense, "gift", "#14b8a6"),
    ];

    private readonly ICategoryRepository _categories;

    public DefaultCategoriesSeeder(ICategoryRepository categories)
    {
        _categories = categories;
    }

    public async Task<int> EnsureAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _categories.ListAsync(cancellationToken);
        var keys = existing
            .Select(category => Key(category.Name, category.Subcategory, category.Type))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var definition in Defaults)
        {
            if (!keys.Add(Key(definition.Name, definition.Subcategory, definition.Type)))
            {
                continue;
            }

            await _categories.AddAsync(
                new Category(
                    Guid.NewGuid(),
                    definition.Name,
                    definition.Type,
                    subcategory: definition.Subcategory,
                    icon: definition.Icon,
                    colorHex: definition.ColorHex),
                cancellationToken);
            added++;
        }

        return added;
    }

    private static string Key(string name, string? subcategory, CategoryType type) =>
        $"{type}|{name.Trim()}|{subcategory?.Trim()}";
}
