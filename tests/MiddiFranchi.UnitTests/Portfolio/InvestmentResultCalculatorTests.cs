using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class InvestmentResultCalculatorTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Instrument Etf = new(
        Guid.Parse("b1f6c1a9-d3a4-4c5e-9f2a-6c6b5d0e1a2b"), "ETF globale", AssetClass.Equity, Euro, PriceUnit.PerUnit);
    private static readonly Account Account = new(
        Guid.Parse("d2f104e2-26bd-46dc-9db3-b83c059fe0a4"), "Conto titoli", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly DateOnly AsOf = new(2026, 10, 20);
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Calculate_ShouldComputeRealizedUnrealizedIncomeAndTotal()
    {
        var buy = Buy(2m, 80m, new DateOnly(2026, 10, 5));
        var sell = Sell(1m, 100m, new DateOnly(2026, 10, 10));
        var dividend = Dividend(5m, new DateOnly(2026, 10, 12));
        var price = Price(90m, new DateOnly(2026, 10, 15));

        var result = InvestmentResultCalculator
            .Calculate([buy, sell, dividend], [price], AsOf)
            .Assets
            .ShouldHaveSingleItem();

        result.Quantity!.Value.Value.ShouldBe(1m);
        result.CostBasis.ShouldBe(new Money(80m, Euro));
        result.RealizedResult.ShouldBe(new Money(20m, Euro));
        result.Income.ShouldBe(new Money(5m, Euro));
        result.CurrentValue.ShouldBe(new Money(90m, Euro));
        result.UnrealizedResult.ShouldBe(new Money(10m, Euro));
        result.TotalResult.ShouldBe(new Money(35m, Euro));
        result.IsComplete.ShouldBeTrue();
    }

    [Fact]
    public void Calculate_ShouldKeepAClosedPositionAndItsRealizedResult()
    {
        var buy = Buy(1m, 80m, new DateOnly(2026, 10, 5));
        var sell = Sell(1m, 100m, new DateOnly(2026, 10, 10));

        var result = InvestmentResultCalculator
            .Calculate([buy, sell], [], AsOf)
            .Assets
            .ShouldHaveSingleItem();

        result.Quantity.ShouldBeNull();
        result.CostBasis.ShouldBe(new Money(0m, Euro));
        result.RealizedResult.ShouldBe(new Money(20m, Euro));
        result.CurrentValue.ShouldBe(new Money(0m, Euro));
        result.UnrealizedResult.ShouldBe(new Money(0m, Euro));
        result.TotalResult.ShouldBe(new Money(20m, Euro));
        result.IsComplete.ShouldBeTrue();
        result.IsOpen.ShouldBeFalse();
    }

    [Fact]
    public void Calculate_ShouldLeaveTheTotalNullWhenAnOpenPositionHasNoQuote()
    {
        var buy = Buy(1m, 80m, new DateOnly(2026, 10, 5));

        var result = InvestmentResultCalculator
            .Calculate([buy], [], AsOf)
            .Assets
            .ShouldHaveSingleItem();

        result.Quantity!.Value.Value.ShouldBe(1m);
        result.CostBasis.ShouldBe(new Money(80m, Euro));
        result.CurrentValue.ShouldBeNull();
        result.UnrealizedResult.ShouldBeNull();
        result.TotalResult.ShouldBeNull();
        result.IsComplete.ShouldBeFalse();
        result.IncompletenessReason.ShouldContain("quotazione");
    }

    [Fact]
    public void Calculate_ShouldIncludeSellFeesInTheRealizedResult()
    {
        var buy = Buy(1m, 80m, new DateOnly(2026, 10, 5));
        var sellFee = new FeeComponent(
            FeeKind.SellFee,
            new PositiveMoney(new Money(2m, Euro)),
            Account);
        var sell = Sell(1m, 100m, new DateOnly(2026, 10, 10), [sellFee]);

        var result = InvestmentResultCalculator
            .Calculate([buy, sell], [], AsOf)
            .Assets
            .ShouldHaveSingleItem();

        result.RealizedResult.ShouldBe(new Money(18m, Euro));
        result.TotalResult.ShouldBe(new Money(18m, Euro));
    }

    [Fact]
    public void Calculate_ShouldSubtractAttributedAndUnattributedStandaloneFees()
    {
        var buy = Buy(1m, 80m, new DateOnly(2026, 10, 5));
        var price = Price(100m, new DateOnly(2026, 10, 15));
        var attributedFee = InvestmentOperation.StandaloneFee(
            Guid.NewGuid(),
            Account,
            new PositiveMoney(new Money(0.5m, Euro)),
            new DateOnly(2026, 10, 12),
            RecordedAt,
            Etf);
        var unattributedFee = InvestmentOperation.StandaloneFee(
            Guid.NewGuid(),
            Account,
            new PositiveMoney(new Money(1m, Euro)),
            new DateOnly(2026, 10, 13),
            RecordedAt);

        var results = InvestmentResultCalculator.Calculate(
            [buy, attributedFee, unattributedFee],
            [price],
            AsOf);

        var result = results.Assets.ShouldHaveSingleItem();
        result.OtherFees.ShouldBe(new Money(0.5m, Euro));
        result.TotalResult.ShouldBe(new Money(19.5m, Euro));
        results.UnattributedOtherFees.ShouldBe(new Money(1m, Euro));
        results.OtherFees.ShouldBe(new Money(1.5m, Euro));
        results.TotalResult.ShouldBe(new Money(18.5m, Euro));
    }

    private static InvestmentOperation Buy(decimal quantity, decimal price, DateOnly date) =>
        InvestmentOperation.Buy(
            Guid.NewGuid(),
            Etf,
            Account,
            new Quantity(quantity),
            QuotedPrice.AtUnitPrice(new UnitPrice(new Money(price, Euro))),
            null,
            date,
            RecordedAt);

    private static InvestmentOperation Sell(
        decimal quantity,
        decimal price,
        DateOnly date,
        IReadOnlyList<FeeComponent>? fees = null) =>
        InvestmentOperation.Sell(
            Guid.NewGuid(),
            Etf,
            Account,
            new Quantity(quantity),
            QuotedPrice.AtUnitPrice(new UnitPrice(new Money(price, Euro))),
            fees,
            date,
            RecordedAt);

    private static InvestmentOperation Dividend(decimal amount, DateOnly date) =>
        InvestmentOperation.Dividend(
            Guid.NewGuid(),
            Etf,
            Account,
            new PositiveMoney(new Money(amount, Euro)),
            date,
            RecordedAt);

    private static PriceObservation Price(decimal price, DateOnly validAt) =>
        PriceObservation.AtUnitPrice(
            Guid.NewGuid(),
            Etf,
            new UnitPrice(new Money(price, Euro)),
            validAt,
            RecordedAt,
            "Test");
}
