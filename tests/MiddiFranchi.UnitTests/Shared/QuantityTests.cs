using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Shared;

public class QuantityTests
{
    [Theory]
    [InlineData(1.5)]
    [InlineData(2.5)]
    [InlineData(0.001)]
    [InlineData(1000000)]
    public void Construction_ShouldAcceptPositiveQuantities(double rawValue)
    {
        var value = Convert.ToDecimal(rawValue, System.Globalization.CultureInfo.InvariantCulture);

        new Quantity(value).Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.0001)]
    [InlineData(-1000)]
    public void Construction_ShouldRejectZeroAndNegativeQuantities(double rawValue)
    {
        var value = Convert.ToDecimal(rawValue, System.Globalization.CultureInfo.InvariantCulture);

        var exception = Should.Throw<ArgumentException>(() => new Quantity(value));

        exception.ParamName.ShouldBe("value");
        exception.Message.ShouldContain("positiva");
    }

    [Fact]
    public void Equality_ShouldBeByMagnitude()
    {
        new Quantity(2.5m).ShouldBe(new Quantity(2.5m));
        new Quantity(2.5m).ShouldNotBe(new Quantity(2.6m));
    }

    [Fact]
    public void ToString_ShouldBeInvariantWithDecimalPoint()
    {
        new Quantity(2.5m).ToString().ShouldBe("2.5");
    }
}
