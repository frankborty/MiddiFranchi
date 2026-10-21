using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed class Account
{
    public Account(
        Guid id,
        string name,
        AccountType type,
        Currency currency,
        AccountStatus status,
        string? colorHex = null,
        string? iconDataUrl = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'identificativo del conto non può essere vuoto.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Il nome del conto è obbligatorio.", nameof(name));
        }

        if (currency.Code is null)
        {
            throw new ArgumentException("La valuta del conto è obbligatoria.", nameof(currency));
        }

        Id = id;
        Name = name;
        Type = type;
        Currency = currency;
        Status = status;
        ColorHex = NormalizeColor(colorHex);
        IconDataUrl = NormalizeIcon(iconDataUrl);
    }

    public Guid Id { get; }

    public string Name { get; }

    public AccountType Type { get; }

    public Currency Currency { get; }

    public AccountStatus Status { get; }

    /// <summary>Colore personalizzato del conto nei grafici, nel formato #RRGGBB; null = automatico.</summary>
    public string? ColorHex { get; }

    /// <summary>Icona personalizzata come data URL; null = icona predefinita del tipo o logo del conto.</summary>
    public string? IconDataUrl { get; }

    private static string? NormalizeColor(string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return null;
        }

        var normalized = colorHex.Trim();
        if (normalized.Length != 7 ||
            normalized[0] != '#' ||
            !normalized[1..].All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "Il colore del conto deve essere nel formato #RRGGBB.",
                nameof(colorHex));
        }

        return normalized.ToLowerInvariant();
    }

    private static string? NormalizeIcon(string? iconDataUrl)
    {
        if (string.IsNullOrWhiteSpace(iconDataUrl))
        {
            return null;
        }

        var normalized = iconDataUrl.Trim();
        if (!normalized.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "L'icona del conto deve essere un'immagine.",
                nameof(iconDataUrl));
        }

        if (normalized.Length > 1_000_000)
        {
            throw new ArgumentException(
                "L'icona del conto è troppo grande.",
                nameof(iconDataUrl));
        }

        return normalized;
    }
}
