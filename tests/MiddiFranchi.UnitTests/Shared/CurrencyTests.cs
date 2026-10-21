using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Shared;

public class CurrencyTests
{
    [Theory]
    [InlineData("EUR")]
    [InlineData("eur")]
    [InlineData(" Eur ")]
    public void Parse_ShouldAcceptOnlyEuro(string code)
    {
        Currency.Parse(code).ShouldBe(Currency.Euro);
        Currency.Parse(code).Code.ShouldBe("EUR");
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("XXX")]
    [InlineData("CHF")]
    [InlineData("EURO")]
    [InlineData("EU2")]
    [InlineData("")]
    public void Parse_ShouldRejectEveryOtherCode(string code)
    {
        var exception = Should.Throw<ArgumentException>(() => Currency.Parse(code));

        exception.ParamName.ShouldBe("code");
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_ShouldReturnFalseForEveryOtherCode(string? code)
    {
        var parsed = Currency.TryParse(code, out var currency);

        parsed.ShouldBeFalse();
        currency.Code.ShouldBeNull();
    }

    [Fact]
    public void SupportedCodes_ShouldContainOnlyEuro()
    {
        Currency.SupportedCodes.ShouldBe(["EUR"]);
    }

    [Fact]
    public void ToString_ShouldReturnEuro()
    {
        Currency.Euro.ToString().ShouldBe("EUR");
    }
}
