using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class InvestmentPositionCalculatorTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Instrument Etf = new(
        Guid.Parse("b1f6c1a9-d3a4-4c5e-9f2a-6c6b5d0e1a2b"), "ETF globale", AssetClass.Equity, Euro, PriceUnit.PerUnit);
    private static readonly Instrument Bond = new(
        Guid.Parse("8f31fbe7-4f8f-4b1f-a187-6c4e6f3c7c42"),
        "Obbligazione società",
        AssetClass.Bond,
        Euro,
        PriceUnit.PercentOfNominal,
        new Money(1000m, Euro));
    private static readonly Account SettlementAccount = new(
        Guid.Parse("d2f104e2-26bd-46dc-9db3-b83c059fe0a4"), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly DateOnly AsOf = new(2026, 3, 31);
    private static readonly DateTimeOffset RecordedAt = new(2026, 3, 19, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Positions_ShouldReturnNothingWithoutOperations()
    {
        InvestmentPositionCalculator
            .Positions([], AsOf)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Positions_ShouldReturnTheQuantityOfASingleBuy()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 10));

        var positions = InvestmentPositionCalculator.Positions([operation], AsOf);

        positions.Count.ShouldBe(1);
        positions[0].Instrument.ShouldBe(Etf);
        positions[0].Quantity.ShouldBe(new Quantity(2.5m));
    }

    [Fact]
    public void Positions_ShouldSumMultipleBuysOfTheSameInstrument()
    {
        var positions = InvestmentPositionCalculator.Positions(
            [Buy(Etf, 1m, new DateOnly(2026, 3, 10)), Buy(Etf, 2.5m, new DateOnly(2026, 3, 20))],
            AsOf);

        positions.Single().Quantity.ShouldBe(new Quantity(3.5m));
    }

    [Fact]
    public void Positions_ShouldTrackDifferentInstrumentsSeparately()
    {
        var positions = InvestmentPositionCalculator.Positions(
            [Buy(Etf, 2.5m, new DateOnly(2026, 3, 10)), Buy(Bond, 3m, new DateOnly(2026, 3, 20))],
            AsOf);

        positions.Count.ShouldBe(2);
        positions.Single(position => position.Instrument.Id == Etf.Id).Quantity.ShouldBe(new Quantity(2.5m));
        positions.Single(position => position.Instrument.Id == Bond.Id).Quantity.ShouldBe(new Quantity(3m));
    }

    [Fact]
    public void Positions_ShouldSupportPartialSales()
    {
        var positions = InvestmentPositionCalculator.Positions(
            [Buy(Etf, 2.5m, new DateOnly(2026, 3, 10)), Sell(Etf, 1m, new DateOnly(2026, 3, 20))],
            AsOf);

        positions.Single().Quantity.ShouldBe(new Quantity(1.5m));
    }

    [Fact]
    public void Positions_ShouldNotListInstrumentsFullySold()
    {
        var positions = InvestmentPositionCalculator.Positions(
            [Buy(Etf, 2.5m, new DateOnly(2026, 3, 10)), Sell(Etf, 2.5m, new DateOnly(2026, 3, 20))],
            AsOf);

        positions.ShouldBeEmpty();
    }

    [Fact]
    public void Positions_ShouldRejectASellExceedingTheHeldQuantity()
    {
        var operations = new List<InvestmentOperation>
        {
            Buy(Etf, 1m, new DateOnly(2026, 3, 10)),
            Sell(Etf, 2m, new DateOnly(2026, 3, 20)),
        };

        var exception = Should.Throw<InvalidOperationException>(
            () => InvestmentPositionCalculator.Positions(operations, AsOf));

        exception.Message.ShouldContain("supera la quantità detenuta");
        exception.Message.ShouldContain(Etf.Name);
    }

    [Fact]
    public void Positions_ShouldExcludeOperationsAfterTheRequestedDate()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 4, 10));

        InvestmentPositionCalculator
            .Positions([operation], AsOf)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Positions_ShouldIncludeOperationsExactlyOnTheRequestedDate()
    {
        var operation = Buy(Etf, 2.5m, AsOf);

        var positions = InvestmentPositionCalculator.Positions([operation], AsOf);

        positions.Single().Quantity.ShouldBe(new Quantity(2.5m));
    }

    [Fact]
    public void Positions_ShouldUseTheEconomicDateAndIgnoreRecordedAt()
    {
        var operation = Buy(
            Etf,
            2.5m,
            new DateOnly(2026, 3, 10),
            recordedAt: new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));

        var positions = InvestmentPositionCalculator.Positions([operation], AsOf);

        positions.Single().Quantity.ShouldBe(new Quantity(2.5m));
    }

    [Fact]
    public void Positions_ShouldIgnoreProventsAndFees()
    {
        var operations = new List<InvestmentOperation>
        {
            Buy(Etf, 2.5m, new DateOnly(2026, 3, 10)),
            InvestmentOperation.Dividend(
                Guid.NewGuid(), Etf, SettlementAccount, Eur(10m), new DateOnly(2026, 3, 20), RecordedAt),
            InvestmentOperation.Coupon(
                Guid.NewGuid(), Bond, SettlementAccount, Eur(25m), new DateOnly(2026, 3, 25), RecordedAt),
            InvestmentOperation.StandaloneFee(
                Guid.NewGuid(), SettlementAccount, Eur(0.50m), new DateOnly(2026, 3, 28), RecordedAt),
        };

        var positions = InvestmentPositionCalculator.Positions(operations, AsOf);

        positions.Single().Quantity.ShouldBe(new Quantity(2.5m));
    }

    [Fact]
    public void Positions_ShouldBeIndependentFromTheInputOrder()
    {
        var positions = InvestmentPositionCalculator.Positions(
            [
                Buy(Etf, 2.5m, new DateOnly(2026, 3, 20)),
                Buy(Etf, 1m, new DateOnly(2026, 3, 10)),
            ],
            AsOf);

        positions.Single().Quantity.ShouldBe(new Quantity(3.5m));
    }

    [Fact]
    public void Positions_ShouldExcludeReversedOperations()
    {
        var reversedBuy = Buy(Etf, 2.5m, new DateOnly(2026, 3, 10));
        var kept = Buy(Etf, 1m, new DateOnly(2026, 3, 20));
        var materializedPostings = reversedBuy.GeneratePostings()
            .Concat(kept.GeneratePostings())
            .ToArray();

        var reversal = new ReversalOperation(Guid.NewGuid(), reversedBuy, new DateOnly(2026, 4, 5), RecordedAt);
        reversal.Id.ShouldNotBe(Guid.Empty);

        var positions = InvestmentPositionCalculator.Positions([reversedBuy, kept], AsOf);

        positions.Single().Quantity.ShouldBe(new Quantity(1m));
        AccountBalanceCalculator
            .Balance(SettlementAccount, materializedPostings, AsOf)
            .ShouldBe(new Money(-80m, Euro));
    }

    [Fact]
    public void Positions_ShouldApplySameDateOperationsInAnExplicitStableOrder()
    {
        var buy = Buy(Etf, 10m, AsOf, recordedAt: new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero));
        var sell = Sell(Etf, 5m, AsOf, recordedAt: new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero));

        var positions = InvestmentPositionCalculator.Positions([buy, sell], AsOf);

        positions.Single().Quantity.ShouldBe(new Quantity(5m));
    }

    [Fact]
    public void Positions_ShouldRejectASellOrderedBeforeItsBuyOnTheSameDate()
    {
        var sell = Sell(Etf, 5m, AsOf, recordedAt: new DateTimeOffset(2026, 3, 10, 7, 0, 0, TimeSpan.Zero));
        var buy = Buy(Etf, 10m, AsOf, recordedAt: new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero));

        Should.Throw<InvalidOperationException>(
            () => InvestmentPositionCalculator.Positions([buy, sell], AsOf))
            .Message.ShouldContain("supera la quantità detenuta");
    }

    [Fact]
    public void Positions_ShouldRejectANullOperationList()
    {
        Should.Throw<ArgumentNullException>(
            () => InvestmentPositionCalculator.Positions(null!, AsOf));
    }

    [Fact]
    public void Positions_ShouldRejectANullOperationElement()
    {
        Should.Throw<ArgumentNullException>(
                () => InvestmentPositionCalculator.Positions([null!], AsOf))
            .ParamName.ShouldBe("operations");
    }

    [Fact]
    public void Positions_ShouldRejectDuplicateOperationIdentifiers()
    {
        var operation = Buy(Etf, 1m, new DateOnly(2026, 3, 10));

        var exception = Should.Throw<ArgumentException>(
            () => InvestmentPositionCalculator.Positions([operation, operation], AsOf));

        exception.ParamName.ShouldBe("operations");
        exception.Message.ShouldContain("presente più volte");
    }

    private static InvestmentOperation Buy(
        Instrument instrument,
        decimal quantity,
        DateOnly date,
        DateTimeOffset? recordedAt = null) =>
        InvestmentOperation.Buy(
            Guid.NewGuid(),
            instrument,
            SettlementAccount,
            new Quantity(quantity),
            Quote(instrument, 80m),
            null,
            date,
            recordedAt ?? RecordedAt);

    private static InvestmentOperation Sell(
        Instrument instrument,
        decimal quantity,
        DateOnly date,
        DateTimeOffset? recordedAt = null) =>
        InvestmentOperation.Sell(
            Guid.NewGuid(),
            instrument,
            SettlementAccount,
            new Quantity(quantity),
            Quote(instrument, 95m),
            null,
            date,
            recordedAt ?? RecordedAt);

    private static QuotedPrice Quote(Instrument instrument, decimal price) =>
        instrument.PriceUnit == PriceUnit.PercentOfNominal
            ? QuotedPrice.AsPercentOfNominal(price)
            : QuotedPrice.AtUnitPrice(new UnitPrice(new Money(price, instrument.QuoteCurrency)));

    private static PositiveMoney Eur(decimal amount) => new(new Money(amount, Euro));
}
