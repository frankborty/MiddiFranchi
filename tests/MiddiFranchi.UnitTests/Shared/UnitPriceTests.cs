using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Shared;

public class UnitPriceTests
{
    private static readonly Currency Euro = Currency.Parse("EUR");
    private static readonly Currency Usd = Currency.Parse("USD");
    private static readonly Quantity OneUnit = new(1m);

    [Fact]
    public void Construction_ShouldAcceptPositivePricesWithCurrency()
    {
        var unitPrice = new UnitPrice(new Money(80m, Euro));

        unitPrice.Price.ShouldBe(new Money(80m, Euro));
        unitPrice.Price.Currency.ShouldBe(Euro);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Construction_ShouldRejectZeroAndNegativePrices(double rawValue)
    {
        var amount = Convert.ToDecimal(rawValue, System.Globalization.CultureInfo.InvariantCulture);

        var exception = Should.Throw<ArgumentException>(
            () => new UnitPrice(new Money(amount, Euro)));

        exception.ParamName.ShouldBe("price");
        exception.Message.ShouldContain("positivo");
    }

    [Fact]
    public void Equality_ShouldBeByPrice()
    {
        new UnitPrice(new Money(80m, Euro)).ShouldBe(new UnitPrice(new Money(80m, Euro)));
        new UnitPrice(new Money(80m, Euro)).ShouldNotBe(new UnitPrice(new Money(80m, Usd)));
        new UnitPrice(new Money(80m, Euro)).ShouldNotBe(new UnitPrice(new Money(81m, Euro)));
    }

    [Fact]
    public void Multiplication_ShouldProduceMoneyInThePriceCurrency()
    {
        var unitPrice = new UnitPrice(new Money(80m, Euro));
        var quantity = new Quantity(2.5m);

        (unitPrice * quantity).ShouldBe(new Money(200m, Euro));
        (quantity * unitPrice).ShouldBe(new Money(200m, Euro));
        (unitPrice * OneUnit).ShouldBe(new Money(80m, Euro));
    }

    [Fact]
    public void Multiplication_ShouldNotRoundPartialProducts()
    {
        var unitPrice = new UnitPrice(new Money(0.335m, Usd));
        var quantity = new Quantity(3m);

        (unitPrice * quantity).ShouldBe(new Money(1.005m, Usd));
    }

    [Fact]
    public void Multiplication_ShouldRejectDefaultValueObjects()
    {
        Should.Throw<InvalidOperationException>(() => default(UnitPrice) * new Quantity(1m));
        Should.Throw<InvalidOperationException>(
            () => new UnitPrice(new Money(1m, Euro)) * default(Quantity));
    }

    [Fact]
    public void ToString_ShouldReuseMoneyRepresentation()
    {
        new UnitPrice(new Money(95m, Euro)).ToString().ShouldBe("95 EUR");
    }
}
