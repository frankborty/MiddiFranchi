namespace MiddiFranchi.Domain.Shared;

public readonly record struct Currency
{
    public const char CurrencySentinel = '\0';

    private static readonly HashSet<string> SupportedCodes = new(StringComparer.Ordinal)
    {
        "EUR",
        "USD",
        "GBP",
        "JPY",
    };

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
                $"Valuta non supportata: '{code}'. Valute supportate: {string.Join(", ", SupportedCodes.OrderBy(code => code))}.",
                nameof(code));
        }

        return currency;
    }

    public static bool TryParse(string? code, out Currency currency)
    {
        var normalized = code?.ToUpperInvariant();

        if (SupportedCodes.Contains(normalized!))
        {
            currency = new Currency(normalized!);
            return true;
        }

        currency = default;
        return false;
    }

    public override string ToString() => Code;
}
