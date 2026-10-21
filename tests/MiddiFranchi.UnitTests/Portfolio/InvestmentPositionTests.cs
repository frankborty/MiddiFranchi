using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class InvestmentPositionTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly InvestmentPortfolio Portfolio = new(
        Guid.NewGuid(), "Portafoglio principale", Euro);
    private static readonly Instrument Etf = new(
        Guid.NewGuid(), "ETF globale", InstrumentType.Etf, Euro, PriceUnit.PerUnit);

    [Fact]
    public void Construction_ShouldAcceptAPositivePosition()
    {
        var position = new InvestmentPosition(Portfolio, Etf, new Quantity(2.5m));

        position.Portfolio.ShouldBe(Portfolio);
        position.Instrument.ShouldBe(Etf);
        position.Quantity.ShouldBe(new Quantity(2.5m));
    }

    [Fact]
    public void Construction_ShouldRejectNullReferences()
    {
        Should.Throw<ArgumentNullException>(
                () => new InvestmentPosition(null!, Etf, new Quantity(1m)))
            .ParamName.ShouldBe("portfolio");

        Should.Throw<ArgumentNullException>(
                () => new InvestmentPosition(Portfolio, null!, new Quantity(1m)))
            .ParamName.ShouldBe("instrument");
    }

    [Fact]
    public void Construction_ShouldRejectADefaultQuantity()
    {
        Should.Throw<ArgumentException>(
                () => new InvestmentPosition(Portfolio, Etf, default))
            .ParamName.ShouldBe("quantity");
    }
}
