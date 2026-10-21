namespace MiddiFranchi.Domain.Shared;

/// <summary>
/// Valuta dell'applicazione. Tutti gli importi sono sempre in EUR: non esiste supporto
/// per altre valute né per conversioni.
/// </summary>
public readonly record struct Currency
{
    public const char CurrencySentinel = '\0';
    public const string EuroCode = "EUR";

    public static Currency Euro { get; } = new(EuroCode);

    public static IReadOnlyList<string> SupportedCodes { get; } = [EuroCode];

    private Currency(string code)
    {
        Code = code;
    }

    public string Code { get; }

    public static Currency Parse(string? code)
    {
        if (!TryParse(code, out var currency))
        {
            throw new ArgumentException(
                $"Valuta non supportata: '{code}'. L'applicazione usa solo EUR.",
                nameof(code));
        }

        return currency;
    }

    public static bool TryParse(string? code, out Currency currency)
    {
        if (string.Equals(code?.Trim(), EuroCode, StringComparison.OrdinalIgnoreCase))
        {
            currency = Euro;
            return true;
        }

        currency = default;
        return false;
    }

    public override string ToString() => Code;
}
