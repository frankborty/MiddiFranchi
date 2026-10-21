using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class QuotedPriceTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Currency Usd = Currency.Euro;
    private static readonly Instrument Etf = new(
        Guid.Parse("b1f6c1a9-d3a4-4c5e-9f2a-6c6b5d0e1a2b"), "ETF globale", InstrumentType.Etf, Euro, PriceUnit.PerUnit);
    private static readonly Instrument Bond = new(
        Guid.Parse("8f31fbe7-4f8f-4b1f-a187-6c4e6f3c7c42"),
        "Obbligazione società",
        InstrumentType.Bond,
        Euro,
        PriceUnit.PercentOfNominal,
        new Money(1000m, Euro));

    [Fact]
    public void AtUnitPrice_ShouldKeepTheMonetaryPricePerUnit()
    {
        var quoted = QuotedPrice.AtUnitPrice(new UnitPrice(new Money(80m, Euro)));

        quoted.Unit.ShouldBe(PriceUnit.PerUnit);
        quoted.PricePerUnit.ShouldBe(new UnitPrice(new Money(80m, Euro)));
        quoted.PercentOfNominal.ShouldBeNull();
        quoted.EffectiveUnitPrice(Etf).ShouldBe(new UnitPrice(new Money(80m, Euro)));
    }

    [Fact]
    public void AsPercentOfNominal_ShouldComputeTheEffectiveUnitPriceFromTheNotional()
    {
        var quoted = QuotedPrice.AsPercentOfNominal(98.5m);

        quoted.Unit.ShouldBe(PriceUnit.PercentOfNominal);
        quoted.PricePerUnit.ShouldBeNull();
        quoted.PercentOfNominal.ShouldBe(98.5m);
        quoted.EffectiveUnitPrice(Bond).ShouldBe(new UnitPrice(new Money(985m, Euro)));
    }

    [Fact]
    public void AsPercentOfNominal_ShouldAllowQuotesAbovePar()
    {
        QuotedPrice.AsPercentOfNominal(102.5m)
            .EffectiveUnitPrice(Bond)
            .ShouldBe(new UnitPrice(new Money(1025m, Euro)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void AsPercentOfNominal_ShouldRejectNonPositiveQuotes(double rawPercent)
    {
        var percent = Convert.ToDecimal(rawPercent, System.Globalization.CultureInfo.InvariantCulture);

        Should.Throw<ArgumentException>(() => QuotedPrice.AsPercentOfNominal(percent))
            .ParamName.ShouldBe("percent");
    }

    [Fact]
    public void EffectiveUnitPrice_ShouldRejectANullInstrument()
    {
        Should.Throw<ArgumentNullException>(
            () => QuotedPrice.AsPercentOfNominal(98.5m).EffectiveUnitPrice(null!))
            .ParamName.ShouldBe("instrument");
    }

    [Fact]
    public void EffectiveUnitPrice_ShouldRequireTheInstrumentPriceUnitToMatch()
    {
        Should.Throw<ArgumentException>(
                () => QuotedPrice.AsPercentOfNominal(98.5m).EffectiveUnitPrice(Etf))
            .ParamName.ShouldBe("instrument");

        Should.Throw<ArgumentException>(
                () => QuotedPrice.AtUnitPrice(new UnitPrice(new Money(985m, Euro))).EffectiveUnitPrice(Bond))
            .ParamName.ShouldBe("instrument");
    }

}
