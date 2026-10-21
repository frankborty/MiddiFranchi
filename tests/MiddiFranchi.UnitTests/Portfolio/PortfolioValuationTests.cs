using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class PortfolioValuationTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly InvestmentPortfolio Portfolio = new(
        Guid.NewGuid(), "Portafoglio principale", Euro);

    [Fact]
    public void Construction_ShouldCopyThePositionsCollection()
    {
        var positions = new List<InvestmentPositionValuation>();

        var valuation = new PortfolioValuation(Portfolio, new DateOnly(2026, 3, 31), positions);

        ReferenceEquals(valuation.Positions, positions).ShouldBeFalse();

        positions.Add(null!);

        valuation.Positions.ShouldBeEmpty();
    }

    [Fact]
    public void Construction_ShouldRejectNullArguments()
    {
        Should.Throw<ArgumentNullException>(
            () => new PortfolioValuation(null!, new DateOnly(2026, 3, 31), []));

        Should.Throw<ArgumentNullException>(
            () => new PortfolioValuation(Portfolio, new DateOnly(2026, 3, 31), null!));
    }
}
