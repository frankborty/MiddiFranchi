using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class InvestmentPositionTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Instrument Etf = new(
        Guid.NewGuid(), "ETF globale", AssetClass.Equity, Euro, PriceUnit.PerUnit);

    [Fact]
    public void Construction_ShouldAcceptAPositivePosition()
    {
        var position = new InvestmentPosition(Etf, new Quantity(2.5m));

        position.Instrument.ShouldBe(Etf);
        position.Quantity.ShouldBe(new Quantity(2.5m));
    }

    [Fact]
    public void Construction_ShouldRejectANullInstrument()
    {
        Should.Throw<ArgumentNullException>(
                () => new InvestmentPosition(null!, new Quantity(1m)))
            .ParamName.ShouldBe("instrument");
    }

    [Fact]
    public void Construction_ShouldRejectADefaultQuantity()
    {
        Should.Throw<ArgumentException>(
                () => new InvestmentPosition(Etf, default))
            .ParamName.ShouldBe("quantity");
    }
}
