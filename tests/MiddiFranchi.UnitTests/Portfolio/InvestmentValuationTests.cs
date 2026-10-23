using MiddiFranchi.Domain.Portfolio;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class InvestmentValuationTests
{
    [Fact]
    public void Construction_ShouldCopyThePositionsCollection()
    {
        var positions = new List<InvestmentPositionValuation>();

        var valuation = new InvestmentValuation(new DateOnly(2026, 3, 31), positions);

        ReferenceEquals(valuation.Positions, positions).ShouldBeFalse();

        positions.Add(null!);

        valuation.Positions.ShouldBeEmpty();
    }

    [Fact]
    public void Construction_ShouldRejectNullPositions()
    {
        Should.Throw<ArgumentNullException>(
            () => new InvestmentValuation(new DateOnly(2026, 3, 31), null!));
    }
}
