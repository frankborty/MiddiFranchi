using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Shared;

public class ExchangeRateTests
{
    private static readonly Currency Euro = Currency.Parse("EUR");
    private static readonly Currency Usd = Currency.Parse("USD");
    private static readonly Currency Gbp = Currency.Parse("GBP");

    [Theory]
    [InlineData(1.10)]
    [InlineData(1.25)]
    [InlineData(0.5)]
    public void Construction_ShouldAcceptPositiveRatesForForeignCurrencies(double rawRate)
    {
        var rate = Convert.ToDecimal(rawRate, System.Globalization.CultureInfo.InvariantCulture);

        var exchangeRate = new ExchangeRate(Usd, rate);

        exchangeRate.Currency.ShouldBe(Usd);
        exchangeRate.Rate.ShouldBe(rate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-1)]
    public void Construction_ShouldRejectZeroAndNegativeRates(double rawRate)
    {
        var rate = Convert.ToDecimal(rawRate, System.Globalization.CultureInfo.InvariantCulture);

        var exception = Should.Throw<ArgumentException>(() => new ExchangeRate(Usd, rate));

        exception.ParamName.ShouldBe("rate");
        exception.Message.ShouldContain("positivo");
    }

    [Fact]
    public void Construction_ShouldRejectADefaultCurrency()
    {
        Should.Throw<ArgumentException>(() => new ExchangeRate(default, 1m))
            .ParamName.ShouldBe("currency");
    }

    [Fact]
    public void Construction_ShouldRejectEuroAsQuotedCurrency()
    {
        var exception = Should.Throw<ArgumentException>(() => new ExchangeRate(Euro, 1m));

        exception.ParamName.ShouldBe("currency");
        exception.Message.ShouldContain("EUR");
    }

    [Fact]
    public void Equality_ShouldBeByCurrencyAndRate()
    {
        new ExchangeRate(Usd, 1.10m).ShouldBe(new ExchangeRate(Usd, 1.10m));
        new ExchangeRate(Usd, 1.10m).ShouldNotBe(new ExchangeRate(Usd, 1.20m));
        new ExchangeRate(Usd, 1.10m).ShouldNotBe(new ExchangeRate(Gbp, 1.10m));
    }

    [Fact]
    public void Conversion_FromEur_ShouldMultiplyByTheRate()
    {
        var exchangeRate = new ExchangeRate(Usd, 1.10m);

        exchangeRate.Convert(new Money(100m, Euro), Usd).ShouldBe(new Money(110m, Usd));
    }

    [Fact]
    public void Conversion_ToEur_ShouldDivideByTheRate()
    {
        var exchangeRate = new ExchangeRate(Usd, 1.10m);

        exchangeRate.Convert(new Money(110m, Usd), Euro).ShouldBe(new Money(100m, Euro));
    }

    [Fact]
    public void Conversion_ShouldPreserveDecimalPrecisionWithoutImplicitRounding()
    {
        var exchangeRate = new ExchangeRate(Usd, 1.335m);

        exchangeRate.Convert(new Money(1m, Euro), Usd).ShouldBe(new Money(1.335m, Usd));
        exchangeRate.Convert(new Money(1.335m, Usd), Euro).ShouldBe(new Money(1m, Euro));
    }

    [Fact]
    public void Conversion_ShouldUseTheRateAdvertisedByTheConvention()
    {
        var exchangeRate = new ExchangeRate(Usd, 1.25m);

        exchangeRate.Convert(new Money(12.50m, Usd), Euro).ShouldBe(new Money(10m, Euro));
    }

    [Fact]
    public void Conversion_ShouldRejectADefaultRate()
    {
        Should.Throw<InvalidOperationException>(
            () => default(ExchangeRate).Convert(new Money(10m, Usd), Euro));
    }

    [Fact]
    public void Conversion_ForEqualCurrencies_ShouldReturnTheAmountUnchanged()
    {
        var exchangeRate = new ExchangeRate(Usd, 1.10m);

        exchangeRate.Convert(new Money(5m, Usd), Usd).ShouldBe(new Money(5m, Usd));
    }

    [Fact]
    public void Conversion_ForEurToEur_ShouldNotApplyAnyRate()
    {
        var exchangeRate = new ExchangeRate(Usd, 1.10m);

        exchangeRate.Convert(new Money(7m, Euro), Euro).ShouldBe(new Money(7m, Euro));
    }

    [Theory]
    [MemberData(nameof(IncoherentConversions))]
    public void Conversion_ShouldRejectIncoherentCurrencyCombinations(
        Money amount, Currency target)
    {
        var exchangeRate = new ExchangeRate(Usd, 1.10m);

        Should.Throw<InvalidOperationException>(
            () => exchangeRate.Convert(amount, target));
    }

    public static IEnumerable<object[]> IncoherentConversions()
    {
        yield return [new Money(10m, Gbp), Euro];
        yield return [new Money(10m, Euro), Gbp];
        yield return [new Money(10m, Usd), Gbp];
        yield return [new Money(10m, Gbp), Usd];
    }

    [Fact]
    public void ToString_ShouldStateRateAndQuotedCurrencyAgainstEur()
    {
        new ExchangeRate(Usd, 1.10m).ToString().ShouldBe("1.10 USD per EUR");
    }
}
