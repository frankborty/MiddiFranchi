using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class InstrumentTests
{
    private static readonly Currency Euro = Currency.Parse("EUR");
    private static readonly Currency Usd = Currency.Parse("USD");

    [Fact]
    public void Construction_ShouldAcceptAnEtfQuotedPerUnit()
    {
        var instrument = new Instrument(
            Guid.Parse("b1f6c1a9-d3a4-4c5e-9f2a-6c6b5d0e1a2b"),
            "ETF globale",
            InstrumentType.Etf,
            Euro,
            PriceUnit.PerUnit);

        instrument.Type.ShouldBe(InstrumentType.Etf);
        instrument.QuoteCurrency.ShouldBe(Euro);
        instrument.PriceUnit.ShouldBe(PriceUnit.PerUnit);
        instrument.NotionalValue.ShouldBeNull();
    }

    [Fact]
    public void Construction_ShouldAcceptABondQuotedAsPercentOfNominal()
    {
        var instrument = new Instrument(
            Guid.Parse("c8e4a2b7-71d5-4f30-a9c6-2bd7fbcf7a94"),
            "Obbligazione società",
            InstrumentType.Bond,
            Euro,
            PriceUnit.PercentOfNominal,
            new Money(1000m, Euro));

        instrument.PriceUnit.ShouldBe(PriceUnit.PercentOfNominal);
        instrument.NotionalValue.ShouldBe(new Money(1000m, Euro));
    }

    [Fact]
    public void Construction_ShouldRequireNotionalValueWhenQuotedAsPercentOfNominal()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Instrument(
                Guid.NewGuid(), "Obbligazione società", InstrumentType.Bond, Euro, PriceUnit.PercentOfNominal, null));

        exception.ParamName.ShouldBe("notionalValue");
        exception.Message.ShouldContain("percentuale del nominale");
    }

    [Fact]
    public void Construction_ShouldRejectNotionalValueWhenQuotedPerUnit()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Instrument(
                Guid.NewGuid(), "ETF globale", InstrumentType.Etf, Euro, PriceUnit.PerUnit, new Money(1000m, Euro)));

        exception.ParamName.ShouldBe("notionalValue");
        exception.Message.ShouldContain("percentuale del nominale");
    }

    [Fact]
    public void Construction_ShouldRejectNotionalValueInADifferentCurrency()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Instrument(
                Guid.NewGuid(),
                "Obbligazione società",
                InstrumentType.Bond,
                Euro,
                PriceUnit.PercentOfNominal,
                new Money(1000m, Usd)));

        exception.ParamName.ShouldBe("notionalValue");
        exception.Message.ShouldContain("coincidere");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Construction_ShouldRejectNonPositiveNotionalValue(double rawAmount)
    {
        var amount = Convert.ToDecimal(rawAmount, System.Globalization.CultureInfo.InvariantCulture);

        var exception = Should.Throw<ArgumentException>(
            () => new Instrument(
                Guid.NewGuid(),
                "Obbligazione società",
                InstrumentType.Bond,
                Euro,
                PriceUnit.PercentOfNominal,
                new Money(amount, Euro)));

        exception.Message.ShouldContain("positivo");
    }

    [Fact]
    public void Construction_ShouldRejectAnEmptyIdentifier()
    {
        Should.Throw<ArgumentException>(
            () => new Instrument(Guid.Empty, "ETF globale", InstrumentType.Etf, Euro, PriceUnit.PerUnit))
            .ParamName.ShouldBe("id");
    }

    [Fact]
    public void Construction_ShouldRejectADefaultQuoteCurrency()
    {
        Should.Throw<ArgumentException>(
            () => new Instrument(Guid.NewGuid(), "ETF globale", InstrumentType.Etf, default, PriceUnit.PerUnit))
            .ParamName.ShouldBe("quoteCurrency");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Construction_ShouldRejectABlankName(string? name)
    {
        Should.Throw<ArgumentException>(
            () => new Instrument(Guid.NewGuid(), name!, InstrumentType.Etf, Euro, PriceUnit.PerUnit))
            .ParamName.ShouldBe("name");
    }
}
