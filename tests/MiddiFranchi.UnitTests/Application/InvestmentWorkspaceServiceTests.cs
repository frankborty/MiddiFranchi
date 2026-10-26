using MiddiFranchi.Application.Financial;
using MiddiFranchi.Application.Shared;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class InvestmentWorkspaceServiceTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly DateOnly Today = new(2026, 10, 20);
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeFinancialOperationRepository _operations = new();
    private readonly FakePriceObservationRepository _prices = new();
    private readonly FakeInstrumentRepository _instruments = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly Account _account;
    private readonly InvestmentWorkspaceService _service;

    public InvestmentWorkspaceServiceTests()
    {
        _account = new Account(
            Guid.NewGuid(), "Conto titoli", AccountType.Checking, Euro, AccountStatus.Active);
        _accounts.Accounts.Add(_account);

        var overview = new InvestmentOverviewService(_operations, _prices, () => Today);
        _service = new InvestmentWorkspaceService(
            overview,
            _instruments,
            _prices,
            _operations,
            _accounts,
            () => Today);
    }

    [Fact]
    public async Task LoadAsync_ShouldIncludeInstrumentsWithoutPositions()
    {
        var instrument = AddInstrument("ETF globale");

        var workspace = await _service.LoadAsync();

        var row = workspace.Assets.ShouldHaveSingleItem();
        row.InstrumentId.ShouldBe(instrument.Id);
        row.Quantity.ShouldBeNull();
        row.InvestedCapital.ShouldBeNull();
        row.CurrentValue.ShouldBeNull();
        row.Status.ShouldBe(InstrumentStatus.Active);
        workspace.CatalogCount.ShouldBe(1);
        workspace.ArchivedCount.ShouldBe(0);
        workspace.TotalIncome.ShouldBe(new Money(0m, Euro));
        workspace.NetIncome.ShouldBe(new Money(0m, Euro));
    }

    [Fact]
    public async Task LoadAsync_ShouldBuildMetricsAndAllocation()
    {
        var instrument = AddInstrument("ETF globale");
        AddBuy(instrument, 2m, QuotedPrice.AtUnitPrice(new UnitPrice(new Money(80m, Euro))),
            new DateOnly(2026, 10, 5));
        AddPrice(instrument, 90m, new DateOnly(2026, 10, 15));

        var workspace = await _service.LoadAsync();

        var row = workspace.Assets.ShouldHaveSingleItem();
        row.Quantity!.Value.Value.ShouldBe(2m);
        row.InvestedCapital.ShouldBe(new Money(160m, Euro));
        row.AverageCost.ShouldBe(new Money(80m, Euro));
        row.CurrentValue.ShouldBe(new Money(180m, Euro));
        row.CurrentUnitPrice.ShouldBe(new Money(90m, Euro));
        row.LatestPrice.ShouldBe(90m);
        row.PriceDate.ShouldBe(new DateOnly(2026, 10, 15));
        row.UnrealizedResult.ShouldBe(new Money(20m, Euro));
        row.TotalResult.ShouldBe(new Money(20m, Euro));
        row.TotalResultPercent.ShouldBe(0.125m);
        row.IsPriceStale.ShouldBeFalse();
        row.Income.ShouldBe(new Money(0m, Euro));
        row.IsValuationComplete.ShouldBeTrue();

        var allocation = workspace.Allocation.ShouldHaveSingleItem();
        allocation.AssetClass.ShouldBe(AssetClass.Equity);
        allocation.Value.ShouldBe(new Money(180m, Euro));
        allocation.Share.ShouldBe(1m);
        allocation.AssetCount.ShouldBe(1);
    }

    [Fact]
    public async Task LoadAsync_ShouldIncludeOrdinaryCouponIncomeInTotals()
    {
        var instrument = AddInstrument("BTP Italia");
        _operations.Operations.Add(new Income(
            Guid.NewGuid(),
            account: null,
            new Category(Guid.NewGuid(), "Cedole", CategoryType.Income),
            new PositiveMoney(new Money(12.5m, Euro)),
            new DateOnly(2026, 10, 10),
            RecordedAt,
            instrument: instrument));

        var workspace = await _service.LoadAsync();

        var row = workspace.Assets.ShouldHaveSingleItem();
        row.Income.ShouldBe(new Money(12.5m, Euro));
        workspace.TotalIncome.ShouldBe(new Money(12.5m, Euro));
        workspace.NetIncome.ShouldBe(new Money(12.5m, Euro));
    }

    [Fact]
    public async Task LoadAsync_ShouldSubtractCouponTaxesFromNetIncome()
    {
        var instrument = AddInstrument("BTP Italia");
        var incomeId = Guid.NewGuid();
        _operations.Operations.Add(new Income(
            incomeId,
            account: null,
            new Category(Guid.NewGuid(), "Cedole", CategoryType.Income),
            new PositiveMoney(new Money(12.5m, Euro)),
            new DateOnly(2026, 10, 10),
            RecordedAt,
            instrument: instrument));
        _operations.Operations.Add(new Expense(
            DeterministicId.From("tax-expense", incomeId.ToString("N")),
            account: null,
            new Category(Guid.NewGuid(), "Fees", CategoryType.Expense, null, "Tasse Investimenti"),
            new PositiveMoney(new Money(2.5m, Euro)),
            new DateOnly(2026, 10, 10),
            RecordedAt,
            "Tasse su cedola",
            instrument: instrument));

        var workspace = await _service.LoadAsync();

        workspace.TotalIncome.ShouldBe(new Money(12.5m, Euro));
        workspace.NetIncome.ShouldBe(new Money(10m, Euro));
    }

    [Fact]
    public async Task LoadAsync_ShouldKeepIncomeSeparateFromTheUnrealizedResult()
    {
        var instrument = AddInstrument("ETF globale");
        AddBuy(instrument, 2m, QuotedPrice.AtUnitPrice(new UnitPrice(new Money(80m, Euro))),
            new DateOnly(2026, 10, 5));
        AddPrice(instrument, 90m, new DateOnly(2026, 10, 15));
        AddDividend(instrument, 5m, new DateOnly(2026, 10, 10));

        var workspace = await _service.LoadAsync();

        var row = workspace.Assets.ShouldHaveSingleItem();
        row.Income.ShouldBe(new Money(5m, Euro));
        row.UnrealizedResult.ShouldBe(new Money(20m, Euro));
        row.TotalResult.ShouldBe(new Money(25m, Euro));
        workspace.TotalIncome.ShouldBe(new Money(5m, Euro));
        workspace.NetIncome.ShouldBe(new Money(5m, Euro));
        workspace.Overview.Performance.ShouldBe(new Money(20m, Euro));
    }

    [Fact]
    public async Task LoadAsync_ShouldNotTreatAMissingQuotationAsZero()
    {
        var instrument = AddInstrument("ETF globale");
        AddBuy(instrument, 2m, QuotedPrice.AtUnitPrice(new UnitPrice(new Money(80m, Euro))),
            new DateOnly(2026, 10, 5));

        var workspace = await _service.LoadAsync();

        var row = workspace.Assets.ShouldHaveSingleItem();
        row.InvestedCapital.ShouldBe(new Money(160m, Euro));
        row.CurrentValue.ShouldBeNull();
        row.UnrealizedResult.ShouldBeNull();
        row.TotalResultPercent.ShouldBeNull();
        row.IsValuationComplete.ShouldBeFalse();
        row.Warnings.ShouldContain(warning => warning.Contains("quotazione"));
        workspace.Overview.IsComplete.ShouldBeFalse();
        workspace.Allocation.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadAsync_ShouldFlagAnOldQuotation()
    {
        var instrument = AddInstrument("ETF globale");
        AddBuy(instrument, 1m, QuotedPrice.AtUnitPrice(new UnitPrice(new Money(80m, Euro))),
            new DateOnly(2026, 1, 5));
        AddPrice(instrument, 90m, new DateOnly(2026, 8, 1));

        var workspace = await _service.LoadAsync();

        var row = workspace.Assets.ShouldHaveSingleItem();
        row.CurrentValue.ShouldBe(new Money(90m, Euro));
        row.IsPriceStale.ShouldBeTrue();
        row.Warnings.ShouldContain(warning => warning.Contains("Ultima quotazione"));
    }

    [Fact]
    public async Task LoadAsync_ShouldExposeArchivedInstrumentsAndCountThem()
    {
        AddInstrument("ETF archiviato", InstrumentStatus.Archived);

        var workspace = await _service.LoadAsync();

        workspace.ArchivedCount.ShouldBe(1);
        workspace.CatalogCount.ShouldBe(1);
        workspace.Assets.ShouldHaveSingleItem().IsArchived.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadAsync_ShouldExpressBondAverageCostAlsoAsPercentOfNominal()
    {
        var bond = AddInstrument(
            "BTP 2030",
            notional: new Money(1000m, Euro),
            priceUnit: PriceUnit.PercentOfNominal,
            assetClass: AssetClass.Bond,
            maturityDate: new DateOnly(2030, 1, 1),
            couponRate: 3.5m);
        AddBuy(bond, 2m, QuotedPrice.AsPercentOfNominal(98.5m), new DateOnly(2026, 10, 5));
        _prices.Observations.Add(PriceObservation.AsPercentOfNominal(
            Guid.NewGuid(),
            bond,
            100m,
            new DateOnly(2026, 10, 15),
            RecordedAt,
            "Test"));

        var workspace = await _service.LoadAsync();

        var row = workspace.Assets.ShouldHaveSingleItem();
        row.AssetClass.ShouldBe(AssetClass.Bond);
        row.InvestedCapital.ShouldBe(new Money(1970m, Euro));
        row.AverageCost.ShouldBe(new Money(985m, Euro));
        row.AverageCostPercentOfNominal.ShouldBe(98.5m);
        row.CurrentValue.ShouldBe(new Money(2000m, Euro));
        row.CurrentUnitPrice.ShouldBe(new Money(1000m, Euro));
        row.LatestPrice.ShouldBe(100m);
        row.UnrealizedResult.ShouldBe(new Money(30m, Euro));
        row.TotalResult.ShouldBe(new Money(30m, Euro));
    }

    private Instrument AddInstrument(
        string name,
        InstrumentStatus status = InstrumentStatus.Active,
        Money? notional = null,
        PriceUnit priceUnit = PriceUnit.PerUnit,
        AssetClass assetClass = AssetClass.Equity,
        DateOnly? maturityDate = null,
        decimal? couponRate = null)
    {
        var instrument = new Instrument(
            Guid.NewGuid(),
            name,
            assetClass,
            Euro,
            priceUnit,
            notional,
            status: status,
            maturityDate: maturityDate,
            couponRate: couponRate);
        _instruments.Instruments.Add(instrument);
        return instrument;
    }

    private void AddBuy(
        Instrument instrument,
        decimal quantity,
        QuotedPrice price,
        DateOnly date) =>
        _operations.Operations.Add(InvestmentOperation.Buy(
            Guid.NewGuid(),
            instrument,
            _account,
            new Quantity(quantity),
            price,
            null,
            date,
            RecordedAt));

    private void AddDividend(Instrument instrument, decimal amount, DateOnly date) =>
        _operations.Operations.Add(InvestmentOperation.Dividend(
            Guid.NewGuid(),
            instrument,
            _account,
            new PositiveMoney(new Money(amount, Euro)),
            date,
            RecordedAt));

    private void AddPrice(Instrument instrument, decimal price, DateOnly validAt) =>
        _prices.Observations.Add(PriceObservation.AtUnitPrice(
            Guid.NewGuid(),
            instrument,
            new UnitPrice(new Money(price, Euro)),
            validAt,
            RecordedAt,
            "Test"));
}
