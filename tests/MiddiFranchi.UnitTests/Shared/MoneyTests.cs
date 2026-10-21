using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Shared;

public class MoneyTests
{
    private static readonly Currency Euro = Currency.Euro;

    [Fact]
    public void Equality_ShouldBeByAmount()
    {
        new Money(10.50m, Euro).ShouldBe(new Money(10.50m, Euro));
        new Money(10.50m, Euro).ShouldNotBe(new Money(11.00m, Euro));
    }

    [Fact]
    public void Addition_ShouldSumAmounts()
    {
        var sum = new Money(10.50m, Euro) + new Money(3.50m, Euro);

        sum.ShouldBe(new Money(14.00m, Euro));
    }

    [Fact]
    public void Subtraction_ShouldSubtractAmounts()
    {
        var difference = new Money(3.50m, Euro) - new Money(7.00m, Euro);

        difference.ShouldBe(new Money(-3.50m, Euro));
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
        var negative = new Money(-42.50m, Euro);

        zero.Amount.ShouldBe(0m);
        negative.Amount.ShouldBe(-42.50m);
        negative.Currency.ShouldBe(Euro);
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
