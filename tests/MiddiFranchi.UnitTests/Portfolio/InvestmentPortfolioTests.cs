using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class InvestmentPortfolioTests
{
    private static readonly Currency Euro = Currency.Euro;

    [Fact]
    public void Construction_ShouldAcceptValidPortfolios()
    {
        var portfolio = new InvestmentPortfolio(
            Guid.Parse("9a5f8b83-c3d4-4a01-9f9f-4c8a0d3f1e2d"),
            "Portafoglio principale",
            Euro);

        portfolio.Name.ShouldBe("Portafoglio principale");
        portfolio.ReferenceCurrency.ShouldBe(Euro);
    }

    [Fact]
    public void Construction_ShouldRejectAnEmptyIdentifier()
    {
        Should.Throw<ArgumentException>(
            () => new InvestmentPortfolio(Guid.Empty, "Portafoglio principale", Euro))
            .ParamName.ShouldBe("id");
    }

    [Fact]
    public void Construction_ShouldRejectADefaultReferenceCurrency()
    {
        Should.Throw<ArgumentException>(
            () => new InvestmentPortfolio(Guid.NewGuid(), "Portafoglio principale", default))
            .ParamName.ShouldBe("referenceCurrency");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Construction_ShouldRejectABlankName(string? name)
    {
        Should.Throw<ArgumentException>(
            () => new InvestmentPortfolio(Guid.NewGuid(), name!, Euro))
            .ParamName.ShouldBe("name");
    }
}
