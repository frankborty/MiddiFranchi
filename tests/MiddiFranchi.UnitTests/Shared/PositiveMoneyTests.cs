using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Shared;

public class PositiveMoneyTests
{
    private static readonly Currency Euro = Currency.Euro;

    [Fact]
    public void Construction_ShouldAcceptPositiveAmountsOfAnyCurrency()
    {
        var positive = new PositiveMoney(new Money(0.01m, Euro));

        positive.Value.ShouldBe(new Money(0.01m, Euro));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Construction_ShouldRejectZeroAndNegativeAmounts(double rawAmount)
    {
        var amount = Convert.ToDecimal(rawAmount, System.Globalization.CultureInfo.InvariantCulture);

        var exception = Should.Throw<ArgumentException>(() => new PositiveMoney(new Money(amount, Euro)));

        exception.ParamName.ShouldBe("value");
        exception.Message.ShouldContain("positivo");
    }
}
