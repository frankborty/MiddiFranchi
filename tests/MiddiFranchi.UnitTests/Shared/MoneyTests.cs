using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Shared;

public class MoneyTests
{
    private static readonly Currency Euro = Currency.Parse("EUR");
    private static readonly Currency Usd = Currency.Parse("USD");

    [Fact]
    public void Equality_ShouldBeByAmountAndCurrency()
    {
        new Money(10.50m, Euro).ShouldBe(new Money(10.50m, Euro));
        new Money(10.50m, Euro).ShouldNotBe(new Money(10.50m, Usd));
        new Money(10.50m, Euro).ShouldNotBe(new Money(11.00m, Euro));
    }

    [Fact]
    public void Addition_ShouldSumAmountsInTheSameCurrency()
    {
        var sum = new Money(10.50m, Euro) + new Money(3.50m, Euro);

        sum.ShouldBe(new Money(14.00m, Euro));
    }

    [Theory]
    [MemberData(nameof(DifferentCurrencies))]
    public void Addition_ShouldRejectDifferentCurrencies(Money left, Money right)
    {
        var exception = Should.Throw<InvalidOperationException>(() => left + right);

        exception.Message.ShouldContain("valute diverse");
    }

    public static IEnumerable<object[]> DifferentCurrencies()
    {
        yield return [new Money(1.00m, Euro), new Money(1.00m, Usd)];
        yield return [new Money(-1.00m, Euro), new Money(1.00m, Usd)];
    }

    [Fact]
    public void Subtraction_ShouldSubtractAmountsInTheSameCurrency()
    {
        var difference = new Money(3.50m, Euro) - new Money(7.00m, Euro);

        difference.ShouldBe(new Money(-3.50m, Euro));
    }

    [Fact]
    public void Subtraction_ShouldRejectDifferentCurrencies()
    {
        Should.Throw<InvalidOperationException>(
            () => new Money(5.00m, Euro) - new Money(2.00m, Usd));
    }

    [Fact]
    public void Multiplication_ShouldScaleAmountsKeepingTheCurrency()
    {
        var result = new Money(2.50m, Euro) * 3m;

        result.ShouldBe(new Money(7.50m, Euro));
        (3m * new Money(2.50m, Euro)).ShouldBe(new Money(7.50m, Euro));
        (new Money(2.50m, Euro) * 0m).ShouldBe(new Money(0m, Euro));
    }

    [Fact]
    public void Construction_ShouldAcceptZeroAndNegativeAmounts()
    {
        var zero = new Money(0m, Euro);
        var negative = new Money(-42.50m, Usd);

        zero.Amount.ShouldBe(0m);
        negative.Amount.ShouldBe(-42.50m);
        negative.Currency.ShouldBe(Usd);
    }

    [Fact]
    public void Construction_ShouldRejectADefaultCurrency()
    {
        Should.Throw<ArgumentException>(() => new Money(1m, default))
            .ParamName.ShouldBe("currency");
    }

    [Fact]
    public void ToString_ShouldRepresentAmountAndCurrency()
    {
        new Money(12.34m, Euro).ToString().ShouldBe("12.34 EUR");
    }
}
