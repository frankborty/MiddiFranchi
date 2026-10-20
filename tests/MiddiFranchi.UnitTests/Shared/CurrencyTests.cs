using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Shared;

public class CurrencyTests
{
    [Theory]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    public void Parse_ShouldAcceptSupportedUppercaseCodes(string code)
    {
        Currency.Parse(code).Code.ShouldBe(code);
    }

    [Theory]
    [InlineData("eur")]
    [InlineData("usd")]
    [InlineData("gbp")]
    [InlineData("jpy")]
    public void Parse_ShouldNormalizeLowercaseCodesToUppercase(string code)
    {
        var expected = code.ToUpperInvariant();

        Currency.Parse(code).Code.ShouldBe(expected);
        Currency.Parse(code).ShouldBe(Currency.Parse(expected));
    }

    [Theory]
    [InlineData("XXX")]
    [InlineData("XTS")]
    [InlineData("CHF")]
    [InlineData("EURO")]
    [InlineData("EU2")]
    [InlineData("")]
    [InlineData("   EUR")]
    [InlineData("eur ")]
    public void Parse_ShouldRejectUnsupportedCodes(string code)
    {
        var exception = Should.Throw<ArgumentException>(() => Currency.Parse(code));

        exception.ParamName.ShouldBe("code");
    }

    [Theory]
    [InlineData("XXX")]
    [InlineData("CHF")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_ShouldReturnFalseForUnsupportedCodes(string? code)
    {
        var parsed = Currency.TryParse(code, out var currency);

        parsed.ShouldBeFalse();
        currency.Code.ShouldBeNull();
    }

    [Fact]
    public void Equality_ShouldBeCaseInsensitiveByCode()
    {
        Currency.Parse("EUR").ShouldBe(Currency.Parse("eur"));
        Currency.Parse("EUR").ShouldNotBe(Currency.Parse("USD"));
    }

    [Fact]
    public void ToString_ShouldReturnTheCurrencyCode()
    {
        Currency.Parse("GBP").ToString().ShouldBe("GBP");
    }
}
