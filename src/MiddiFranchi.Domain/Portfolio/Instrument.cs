using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

public sealed class Instrument
{
    public Instrument(
        Guid id,
        string name,
        InstrumentType type,
        Currency quoteCurrency,
        PriceUnit priceUnit,
        Money? notionalValue = null,
        string? symbol = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'identificativo dello strumento non può essere vuoto.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Il nome dello strumento è obbligatorio.", nameof(name));
        }

        if (quoteCurrency.Code is null)
        {
            throw new ArgumentException("La valuta di quotazione dello strumento è obbligatoria.", nameof(quoteCurrency));
        }

        if (priceUnit == PriceUnit.PercentOfNominal)
        {
            ValidateNotionalValue(notionalValue, quoteCurrency);
        }
        else if (notionalValue is not null)
        {
            throw new ArgumentException(
                "Il valore nominale per unità è rilevante solo quando il prezzo unitario è espresso come percentuale del nominale.",
                nameof(notionalValue));
        }

        var normalizedSymbol = string.IsNullOrWhiteSpace(symbol) ? null : symbol.Trim();
        if (normalizedSymbol is { Length: > 32 })
        {
            throw new ArgumentException(
                "La sigla dello strumento non può superare 32 caratteri.",
                nameof(symbol));
        }

        Id = id;
        Name = name;
        Type = type;
        QuoteCurrency = quoteCurrency;
        PriceUnit = priceUnit;
        NotionalValue = notionalValue;
        Symbol = normalizedSymbol;
    }

    public Guid Id { get; }

    public string Name { get; }

    public InstrumentType Type { get; }

    public Currency QuoteCurrency { get; }

    public PriceUnit PriceUnit { get; }

    public Money? NotionalValue { get; }

    /// <summary>Sigla di mercato opzionale (per esempio un ticker); assente se non registrata.</summary>
    public string? Symbol { get; }

    private static void ValidateNotionalValue(Money? notionalValue, Currency quoteCurrency)
    {
        if (notionalValue is null)
        {
            throw new ArgumentException(
                "Il valore nominale per unità è obbligatorio quando il prezzo unitario è espresso come percentuale del nominale.",
                nameof(notionalValue));
        }

        if (notionalValue.Value.Amount <= 0m)
        {
            throw new ArgumentException(
                $"Il valore nominale per unità deve essere positivo, ricevuto {notionalValue.Value}.",
                nameof(notionalValue));
        }

        if (notionalValue.Value.Currency != quoteCurrency)
        {
            throw new ArgumentException(
                $"La valuta del valore nominale ({notionalValue.Value.Currency.Code}) deve coincidere con la valuta di quotazione dello strumento ({quoteCurrency.Code}).",
                nameof(notionalValue));
        }
    }
}
