using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

public sealed record PriceObservation
{
    private PriceObservation(
        Guid id,
        Instrument instrument,
        PriceUnit unit,
        decimal price,
        Currency? priceCurrency,
        DateOnly validAt,
        DateTimeOffset observedAt,
        string source,
        TimeOnly? tradeTime)
    {
        Id = id;
        Instrument = instrument;
        Unit = unit;
        Price = price;
        PriceCurrency = priceCurrency;
        ValidAt = validAt;
        ObservedAt = observedAt;
        Source = source;
        TradeTime = tradeTime;
    }

    public Guid Id { get; }

    public Instrument Instrument { get; }

    public PriceUnit Unit { get; }

    public decimal Price { get; }

    public Currency? PriceCurrency { get; }

    public DateOnly ValidAt { get; }

    public DateTimeOffset ObservedAt { get; }

    public string Source { get; }

    public TimeOnly? TradeTime { get; }

    public static PriceObservation AtUnitPrice(
        Guid id,
        Instrument instrument,
        UnitPrice price,
        DateOnly validAt,
        DateTimeOffset observedAt,
        string source,
        TimeOnly? tradeTime = null)
    {
        Validate(id, instrument, source);

        if (instrument.PriceUnit != PriceUnit.PerUnit)
        {
            throw new ArgumentException(
                "Lo strumento ha prezzo in percentuale del nominale: la quotazione deve essere registrata con la stessa unità.",
                nameof(instrument));
        }

        if (price.Price.Currency != instrument.QuoteCurrency)
        {
            throw new ArgumentException(
                $"La valuta del prezzo ({price.Price.Currency.Code}) deve coincidere con la valuta di quotazione dello strumento ({instrument.QuoteCurrency.Code}).",
                nameof(price));
        }

        return new(
            id,
            instrument,
            PriceUnit.PerUnit,
            price.Price.Amount,
            price.Price.Currency,
            validAt,
            observedAt,
            source,
            tradeTime);
    }

    public static PriceObservation AsPercentOfNominal(
        Guid id,
        Instrument instrument,
        decimal price,
        DateOnly validAt,
        DateTimeOffset observedAt,
        string source,
        TimeOnly? tradeTime = null)
    {
        Validate(id, instrument, source);

        if (instrument.PriceUnit != PriceUnit.PercentOfNominal)
        {
            throw new ArgumentException(
                "Lo strumento ha prezzo per unità: la quotazione deve essere registrata nella stessa unità.",
                nameof(instrument));
        }

        if (price <= 0m)
        {
            throw new ArgumentException(
                $"La quotazione deve essere positiva, ricevuto {price.ToString(System.Globalization.CultureInfo.InvariantCulture)}.",
                nameof(price));
        }

        return new(
            id,
            instrument,
            PriceUnit.PercentOfNominal,
            price,
            null,
            validAt,
            observedAt,
            source,
            tradeTime);
    }

    private static void Validate(Guid id, Instrument instrument, string source)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'identificativo della quotazione non può essere vuoto.", nameof(id));
        }

        if (instrument is null)
        {
            throw new ArgumentNullException(nameof(instrument));
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("La fonte della quotazione è obbligatoria.", nameof(source));
        }
    }
}
