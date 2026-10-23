using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class InvestmentCostBasisCalculatorTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Instrument Etf = new(
        Guid.Parse("b1f6c1a9-d3a4-4c5e-9f2a-6c6b5d0e1a2b"),
        "ETF globale",
        AssetClass.Equity,
        Euro,
        PriceUnit.PerUnit,
        symbol: "V3MA");
    private static readonly Account Account = new(
        Guid.Parse("d2f104e2-26bd-46dc-9db3-b83c059fe0a4"),
        "Conto EUR",
        AccountType.Checking,
        Euro,
        AccountStatus.Active);
    private static readonly DateOnly AsOf = new(2026, 10, 20);
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Calculate_ShouldIncludeBuyFeesInTheWeightedAverageCost()
    {
        var first = Buy(2m, 100m, new DateOnly(2026, 10, 1), [BuyFee(1.50m)]);
        var second = Buy(1m, 120m, new DateOnly(2026, 10, 2), [BuyFee(0.50m)]);

        var cost = InvestmentCostBasisCalculator
            .Calculate([first, second], AsOf)
            .ShouldHaveSingleItem();

        cost.Quantity.ShouldBe(new Quantity(3m));
        cost.CostBasis.ShouldBe(new Money(322m, Euro));
        Math.Round(cost.AverageCost.Amount, 6).ShouldBe(107.333333m);
    }

    [Fact]
    public void Calculate_ShouldReduceQuantityAndCostProportionallyOnPartialSale()
    {
        var buy = Buy(2.5m, 80m, new DateOnly(2026, 10, 1), [BuyFee(1.50m)]);
        var sell = Sell(1m, 95m, new DateOnly(2026, 10, 5));

        var cost = InvestmentCostBasisCalculator
            .Calculate([buy, sell], AsOf)
            .ShouldHaveSingleItem();

        cost.Quantity.ShouldBe(new Quantity(1.5m));
        cost.CostBasis.ShouldBe(new Money(120.90m, Euro));
    }

    [Fact]
    public void Calculate_ShouldIgnoreReversedAndFutureOperations()
    {
        var reversed = Buy(1m, 100m, new DateOnly(2026, 10, 1));
        var kept = Buy(3m, 100m, new DateOnly(2026, 10, 2));
        var future = Buy(2m, 100m, new DateOnly(2026, 10, 25));
        _ = new ReversalOperation(
            Guid.NewGuid(), reversed, new DateOnly(2026, 10, 3), RecordedAt);

        var cost = InvestmentCostBasisCalculator
            .Calculate([reversed, kept, future], AsOf)
            .ShouldHaveSingleItem();

        cost.Quantity.ShouldBe(new Quantity(3m));
        cost.CostBasis.ShouldBe(new Money(300m, Euro));
        cost.Instrument.ShouldBe(Etf);
    }

    [Fact]
    public void Calculate_ShouldReturnNoPositionWhenFullySold()
    {
        var buy = Buy(2m, 80m, new DateOnly(2026, 10, 1));
        var sell = Sell(2m, 90m, new DateOnly(2026, 10, 5));

        InvestmentCostBasisCalculator
            .Calculate([buy, sell], AsOf)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Calculate_ShouldRejectASellExceedingTheHeldQuantity()
    {
        var buy = Buy(1m, 80m, new DateOnly(2026, 10, 1));
        var sell = Sell(2m, 90m, new DateOnly(2026, 10, 5));

        var exception = Should.Throw<InvalidOperationException>(
            () => InvestmentCostBasisCalculator.Calculate([buy, sell], AsOf));

        exception.Message.ShouldContain("supera la quantità detenuta");
        exception.Message.ShouldContain(Etf.Name);
    }

    private static InvestmentOperation Buy(
        decimal quantity,
        decimal price,
        DateOnly date,
        IReadOnlyList<FeeComponent>? fees = null) =>
        InvestmentOperation.Buy(
            Guid.NewGuid(),
            Etf,
            Account,
            new Quantity(quantity),
            QuotedPrice.AtUnitPrice(new UnitPrice(new Money(price, Euro))),
            fees,
            date,
            RecordedAt);

    private static InvestmentOperation Sell(decimal quantity, decimal price, DateOnly date) =>
        InvestmentOperation.Sell(
            Guid.NewGuid(),
            Etf,
            Account,
            new Quantity(quantity),
            QuotedPrice.AtUnitPrice(new UnitPrice(new Money(price, Euro))),
            null,
            date,
            RecordedAt);

    private static FeeComponent BuyFee(decimal amount) =>
        new(
            FeeKind.BuyFee,
            new PositiveMoney(new Money(amount, Euro)),
            Account);
}
