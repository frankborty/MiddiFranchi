using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

public readonly record struct QuotedPrice
{
    private QuotedPrice(PriceUnit unit, UnitPrice? pricePerUnit, decimal? percentOfNominal)
    {
        Unit = unit;
        PricePerUnit = pricePerUnit;
        PercentOfNominal = percentOfNominal;
    }

    public PriceUnit Unit { get; }

    public UnitPrice? PricePerUnit { get; }

    public decimal? PercentOfNominal { get; }

    public static QuotedPrice AtUnitPrice(UnitPrice price) =>
        new(PriceUnit.PerUnit, price, null);

    public static QuotedPrice AsPercentOfNominal(decimal percent)
    {
        if (percent <= 0m)
        {
            throw new ArgumentException(
                $"La quotazione percentuale del nominale deve essere positiva, ricevuto {percent.ToString(System.Globalization.CultureInfo.InvariantCulture)}.",
                nameof(percent));
        }

        return new(PriceUnit.PercentOfNominal, null, percent);
    }

    public UnitPrice EffectiveUnitPrice(Instrument instrument)
    {
        if (instrument is null)
        {
            throw new ArgumentNullException(nameof(instrument));
        }

        if (instrument.PriceUnit != Unit)
        {
            throw new ArgumentException(
                $"Il prezzo quotato è di tipo {Unit}, diverso dall'unità di prezzo dello strumento {instrument.Name} ({instrument.PriceUnit}).",
                nameof(instrument));
        }

        return Unit switch
        {
            PriceUnit.PerUnit => ValidatePerUnitPrice(instrument),
            PriceUnit.PercentOfNominal => EffectiveUnitPriceFromNominal(instrument),
            _ => throw new InvalidOperationException(
                $"L'unità di prezzo {Unit} non è supportata per il prezzo quotato."),
        };
    }

    private UnitPrice ValidatePerUnitPrice(Instrument instrument)
    {
        if (PricePerUnit is null)
        {
            throw new InvalidOperationException("Il prezzo per unità non è stato specificato.");
        }

        var price = PricePerUnit.Value;

        if (price.Price.Currency != instrument.QuoteCurrency)
        {
            throw new ArgumentException(
                $"La valuta del prezzo ({price.Price.Currency.Code}) deve coincidere con la valuta di quotazione dello strumento ({instrument.QuoteCurrency.Code}).",
                nameof(instrument));
        }

        return price;
    }

    private UnitPrice EffectiveUnitPriceFromNominal(Instrument instrument)
    {
        if (PercentOfNominal is null || instrument.NotionalValue is null)
        {
            throw new InvalidOperationException(
                "La quotazione percentuale richiede il valore nominale dello strumento.");
        }

        var notional = instrument.NotionalValue.Value;
        return new UnitPrice(new Money(notional.Amount * PercentOfNominal.Value / 100m, notional.Currency));
    }
}
