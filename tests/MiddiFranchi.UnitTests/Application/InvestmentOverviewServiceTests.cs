using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class InvestmentOverviewServiceTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly DateOnly Today = new(2026, 10, 20);
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoadAsync_ShouldBuildInvestedValueAndPerformance()
    {
        var project = new Project();
        var instrument = Instrument("MSCI World ETF", Euro, "V3MA");
        var account = Account("Conto titoli", Euro);
        project.AddPrice(instrument, 80m, new DateOnly(2026, 10, 1));
        project.AddBuy(instrument, account, 2.5m, 80m, new DateOnly(2026, 10, 5));
        project.AddPrice(instrument, 90m, new DateOnly(2026, 10, 15));

        var overview = await project.Build().LoadAsync();

        overview.IsComplete.ShouldBeTrue();
        overview.InvestedCapital.ShouldBe(new Money(200m, Euro));
        overview.CurrentValue.ShouldBe(new Money(225m, Euro));
        overview.Performance.ShouldBe(new Money(25m, Euro));
        overview.PerformancePercent.ShouldBe(0.125m);
        overview.RealizedResult.ShouldBe(new Money(0m, Euro));
        overview.Income.ShouldBe(new Money(0m, Euro));
        overview.OtherFees.ShouldBe(new Money(0m, Euro));
        overview.TotalResult.ShouldBe(new Money(25m, Euro));
        overview.TotalResultPercent.ShouldBe(0.125m);
        overview.IncompletenessReasons.ShouldBeEmpty();

        var asset = overview.Assets.ShouldHaveSingleItem();
        asset.Name.ShouldBe("MSCI World ETF");
        asset.Symbol.ShouldBe("V3MA");
        asset.InvestedCapital.ShouldBe(new Money(200m, Euro));
        asset.AverageCost.ShouldBe(new Money(80m, Euro));
        asset.CurrentValue.ShouldBe(new Money(225m, Euro));
        asset.UnrealizedResult.ShouldBe(new Money(25m, Euro));
        asset.TotalResult.ShouldBe(new Money(25m, Euro));
        asset.IsComplete.ShouldBeTrue();

        overview.History.IsComplete.ShouldBeTrue();
        overview.History.From.ShouldBe(new DateOnly(2026, 10, 5));
        overview.History.To.ShouldBe(Today);
        overview.History.Currency.ShouldBe(Euro);
        overview.History.Points.Count.ShouldBe(3);
        overview.History.Points[0].Date.ShouldBe(new DateOnly(2026, 10, 5));
        overview.History.Points[0].InvestedCapital.ShouldBe(new Money(200m, Euro));
        overview.History.Points[0].CurrentValue.ShouldBe(new Money(200m, Euro));
        overview.History.Points[^1].CurrentValue.ShouldBe(new Money(225m, Euro));
    }

    [Fact]
    public async Task LoadAsync_ShouldNotTreatAnUnvaluedPositionAsZero()
    {
        var project = new Project();
        var instrument = Instrument("MSCI World ETF", Euro);
        var account = Account("Conto titoli", Euro);
        project.AddBuy(instrument, account, 2m, 80m, new DateOnly(2026, 10, 5));

        var overview = await project.Build().LoadAsync();

        overview.IsComplete.ShouldBeFalse();
        overview.InvestedCapital.ShouldBeNull();
        overview.CurrentValue.ShouldBeNull();
        overview.TotalResult.ShouldBeNull();
        overview.IncompletenessReasons.ShouldContain(reason => reason.Contains("quotazione"));

        var asset = overview.Assets.ShouldHaveSingleItem();
        asset.Symbol.ShouldBeNull();
        asset.InvestedCapital.ShouldBe(new Money(160m, Euro));
        asset.CurrentValue.ShouldBeNull();
        asset.UnrealizedResult.ShouldBeNull();
        asset.TotalResult.ShouldBeNull();
        asset.IsComplete.ShouldBeFalse();
        asset.IncompletenessReason.ShouldNotBeNull().ShouldContain("quotazione");

        overview.History.IsComplete.ShouldBeFalse();
        overview.History.Points.Count.ShouldBe(2);
        overview.History.Points.All(point =>
                point.InvestedCapital == new Money(160m, Euro) &&
                point.CurrentValue is null)
            .ShouldBeTrue();
    }

    [Fact]
    public async Task LoadAsync_ShouldIncludeRealizedGainsIncomeAndTotalResult()
    {
        var project = new Project();
        var instrument = Instrument("MSCI World ETF", Euro);
        var account = Account("Conto titoli", Euro);
        project.AddBuy(instrument, account, 2m, 80m, new DateOnly(2026, 10, 5));
        project.AddSell(instrument, account, 1m, 100m, new DateOnly(2026, 10, 10));
        project.AddDividend(instrument, account, 5m, new DateOnly(2026, 10, 12));
        project.AddPrice(instrument, 90m, new DateOnly(2026, 10, 15));

        var overview = await project.Build().LoadAsync();

        overview.IsComplete.ShouldBeTrue();
        overview.InvestedCapital.ShouldBe(new Money(80m, Euro));
        overview.CurrentValue.ShouldBe(new Money(90m, Euro));
        overview.Performance.ShouldBe(new Money(10m, Euro));
        overview.RealizedResult.ShouldBe(new Money(20m, Euro));
        overview.Income.ShouldBe(new Money(5m, Euro));
        overview.TotalResult.ShouldBe(new Money(35m, Euro));
        overview.TotalResultPercent.ShouldBe(0.4375m);

        var asset = overview.Assets.ShouldHaveSingleItem();
        asset.Quantity.Value.ShouldBe(1m);
        asset.RealizedResult.ShouldBe(new Money(20m, Euro));
        asset.Income.ShouldBe(new Money(5m, Euro));
        asset.UnrealizedResult.ShouldBe(new Money(10m, Euro));
        asset.TotalResult.ShouldBe(new Money(35m, Euro));
    }

    private static Instrument Instrument(string name, Currency currency, string? symbol = null) =>
        new(Guid.NewGuid(), name, AssetClass.Equity, currency, PriceUnit.PerUnit, symbol: symbol);

    private static Account Account(string name, Currency currency) =>
        new(Guid.NewGuid(), name, AccountType.Checking, currency, AccountStatus.Active);

    private sealed class Project
    {
        public FakeFinancialOperationRepository Operations { get; } = new();

        public FakePriceObservationRepository Prices { get; } = new();

        public InvestmentOverviewService Build() => new(
            Operations,
            Prices,
            () => Today);

        public void AddBuy(
            Instrument instrument,
            Account account,
            decimal quantity,
            decimal price,
            DateOnly date)
        {
            Operations.Operations.Add(InvestmentOperation.Buy(
                Guid.NewGuid(),
                instrument,
                account,
                new Quantity(quantity),
                QuotedPrice.AtUnitPrice(new UnitPrice(new Money(price, instrument.QuoteCurrency))),
                null,
                date,
                RecordedAt));
        }

        public void AddSell(
            Instrument instrument,
            Account account,
            decimal quantity,
            decimal price,
            DateOnly date)
        {
            Operations.Operations.Add(InvestmentOperation.Sell(
                Guid.NewGuid(),
                instrument,
                account,
                new Quantity(quantity),
                QuotedPrice.AtUnitPrice(new UnitPrice(new Money(price, instrument.QuoteCurrency))),
                null,
                date,
                RecordedAt));
        }

        public void AddDividend(
            Instrument instrument,
            Account account,
            decimal amount,
            DateOnly date)
        {
            Operations.Operations.Add(InvestmentOperation.Dividend(
                Guid.NewGuid(),
                instrument,
                account,
                new PositiveMoney(new Money(amount, instrument.QuoteCurrency)),
                date,
                RecordedAt));
        }

        public void AddPrice(Instrument instrument, decimal price, DateOnly validAt) =>
            Prices.Observations.Add(PriceObservation.AtUnitPrice(
                Guid.NewGuid(),
                instrument,
                new UnitPrice(new Money(price, instrument.QuoteCurrency)),
                validAt,
                RecordedAt,
                "Test"));
    }
}
