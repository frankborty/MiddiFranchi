using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class InstrumentTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Currency Usd = Currency.Euro;

    [Fact]
    public void Construction_ShouldAcceptAnEtfQuotedPerUnit()
    {
        var instrument = new Instrument(
            Guid.Parse("b1f6c1a9-d3a4-4c5e-9f2a-6c6b5d0e1a2b"),
            "ETF globale",
            AssetClass.Equity,
            Euro,
            PriceUnit.PerUnit);

        instrument.AssetClass.ShouldBe(AssetClass.Equity);
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
            AssetClass.Bond,
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
                Guid.NewGuid(), "Obbligazione società", AssetClass.Bond, Euro, PriceUnit.PercentOfNominal, null));

        exception.ParamName.ShouldBe("notionalValue");
        exception.Message.ShouldContain("percentuale del nominale");
    }

    [Fact]
    public void Construction_ShouldRejectNotionalValueWhenQuotedPerUnit()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Instrument(
                Guid.NewGuid(), "ETF globale", AssetClass.Equity, Euro, PriceUnit.PerUnit, new Money(1000m, Euro)));

        exception.ParamName.ShouldBe("notionalValue");
        exception.Message.ShouldContain("percentuale del nominale");
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
                AssetClass.Bond,
                Euro,
                PriceUnit.PercentOfNominal,
                new Money(amount, Euro)));

        exception.Message.ShouldContain("positivo");
    }

    [Fact]
    public void Construction_ShouldNormalizeAndKeepTheOptionalSymbol()
    {
        var instrument = new Instrument(
            Guid.NewGuid(),
            "ETF globale",
            AssetClass.Equity,
            Euro,
            PriceUnit.PerUnit,
            symbol: "  V3MA  ");

        instrument.Symbol.ShouldBe("V3MA");
    }

    [Fact]
    public void Construction_ShouldTreatABlankSymbolAsAbsent()
    {
        var instrument = new Instrument(
            Guid.NewGuid(),
            "ETF globale",
            AssetClass.Equity,
            Euro,
            PriceUnit.PerUnit,
            symbol: "   ");

        instrument.Symbol.ShouldBeNull();
    }

    [Fact]
    public void Construction_ShouldRejectASymbolLongerThanTheSupportedLength()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Instrument(
                Guid.NewGuid(),
                "ETF globale",
                AssetClass.Equity,
                Euro,
                PriceUnit.PerUnit,
                symbol: new string('A', 33)));

        exception.ParamName.ShouldBe("symbol");
        exception.Message.ShouldContain("32");
    }

    [Fact]
    public void Construction_ShouldRejectAnEmptyIdentifier()
    {
        Should.Throw<ArgumentException>(
            () => new Instrument(Guid.Empty, "ETF globale", AssetClass.Equity, Euro, PriceUnit.PerUnit))
            .ParamName.ShouldBe("id");
    }

    [Fact]
    public void Construction_ShouldRejectADefaultQuoteCurrency()
    {
        Should.Throw<ArgumentException>(
            () => new Instrument(Guid.NewGuid(), "ETF globale", AssetClass.Equity, default, PriceUnit.PerUnit))
            .ParamName.ShouldBe("quoteCurrency");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Construction_ShouldRejectABlankName(string? name)
    {
        Should.Throw<ArgumentException>(
            () => new Instrument(Guid.NewGuid(), name!, AssetClass.Equity, Euro, PriceUnit.PerUnit))
            .ParamName.ShouldBe("name");
    }

    [Fact]
    public void Construction_ShouldNormalizeIsinReferenceUrlAndNotes()
    {
        const string url = "https://www.justetf.com/it/etf-profile.html?isin=IE00B4L5Y983";

        var instrument = new Instrument(
            Guid.NewGuid(),
            "ETF globale",
            AssetClass.Equity,
            Euro,
            PriceUnit.PerUnit,
            isin: "  ie00b4l5y983  ",
            referenceUrl: $"  {url}  ",
            notes: "  PAC mensile  ");

        instrument.Isin.ShouldBe("IE00B4L5Y983");
        instrument.ReferenceUrl.ShouldBe(url);
        instrument.Notes.ShouldBe("PAC mensile");
        instrument.Status.ShouldBe(InstrumentStatus.Active);
        instrument.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("SHORT")]
    [InlineData("IE00B4L5Y98!")]
    [InlineData("IE00B4L5Y9831")]
    public void Construction_ShouldRejectAnInvalidIsin(string isin)
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Instrument(
                Guid.NewGuid(), "ETF globale", AssetClass.Equity, Euro, PriceUnit.PerUnit, isin: isin));

        exception.ParamName.ShouldBe("isin");
        exception.Message.ShouldContain("12");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/scheda")]
    public void Construction_ShouldRejectAnInvalidReferenceUrl(string url)
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Instrument(
                Guid.NewGuid(), "ETF globale", AssetClass.Equity, Euro, PriceUnit.PerUnit, referenceUrl: url));

        exception.ParamName.ShouldBe("referenceUrl");
        exception.Message.ShouldContain("http");
    }

    [Fact]
    public void Construction_ShouldRejectBondDetailsForOtherAssetClasses()
    {
        Should.Throw<ArgumentException>(
                () => new Instrument(
                    Guid.NewGuid(),
                    "ETF globale",
                    AssetClass.Equity,
                    Euro,
                    PriceUnit.PerUnit,
                    maturityDate: new DateOnly(2030, 1, 1)))
            .ParamName.ShouldBe("maturityDate");

        Should.Throw<ArgumentException>(
                () => new Instrument(
                    Guid.NewGuid(),
                    "ETF globale",
                    AssetClass.Equity,
                    Euro,
                    PriceUnit.PerUnit,
                    couponRate: 3.5m))
            .ParamName.ShouldBe("couponRate");
    }

    [Fact]
    public void Construction_ShouldAcceptBondDetailsForBonds()
    {
        var instrument = new Instrument(
            Guid.NewGuid(),
            "BTP 2030",
            AssetClass.Bond,
            Euro,
            PriceUnit.PercentOfNominal,
            new Money(1000m, Euro),
            maturityDate: new DateOnly(2030, 1, 1),
            couponRate: 3.5m);

        instrument.MaturityDate.ShouldBe(new DateOnly(2030, 1, 1));
        instrument.CouponRate.ShouldBe(3.5m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100.1)]
    public void Construction_ShouldRejectACouponRateOutsideTheAllowedRange(double rate)
    {
        var decimalRate = Convert.ToDecimal(rate, System.Globalization.CultureInfo.InvariantCulture);

        var exception = Should.Throw<ArgumentException>(
            () => new Instrument(
                Guid.NewGuid(),
                "BTP 2030",
                AssetClass.Bond,
                Euro,
                PriceUnit.PercentOfNominal,
                new Money(1000m, Euro),
                couponRate: decimalRate));

        exception.ParamName.ShouldBe("couponRate");
    }

    [Fact]
    public void WithStatus_ShouldPreserveTheInstrumentDetails()
    {
        var instrument = new Instrument(
            Guid.NewGuid(),
            "BTP 2030",
            AssetClass.Bond,
            Euro,
            PriceUnit.PercentOfNominal,
            new Money(1000m, Euro),
            symbol: "BTP30",
            isin: "IT0001234567",
            referenceUrl: "https://example.com/btp",
            notes: "Cedola fissa",
            maturityDate: new DateOnly(2030, 1, 1),
            couponRate: 3.5m);

        var archived = instrument.WithStatus(InstrumentStatus.Archived);

        archived.Status.ShouldBe(InstrumentStatus.Archived);
        archived.IsArchived.ShouldBeTrue();
        archived.Id.ShouldBe(instrument.Id);
        archived.Name.ShouldBe(instrument.Name);
        archived.AssetClass.ShouldBe(AssetClass.Bond);
        archived.Isin.ShouldBe("IT0001234567");
        archived.ReferenceUrl.ShouldBe("https://example.com/btp");
        archived.Notes.ShouldBe("Cedola fissa");
        archived.MaturityDate.ShouldBe(new DateOnly(2030, 1, 1));
        archived.CouponRate.ShouldBe(3.5m);
    }
}
