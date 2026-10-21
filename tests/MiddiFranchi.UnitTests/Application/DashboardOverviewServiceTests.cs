using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class DashboardOverviewServiceTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly DateOnly Today = new(2026, 2, 10);
    private static readonly DateTimeOffset RecordedAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeManualBalanceRepository _manualBalances = new();
    private readonly FakeInvestmentPortfolioRepository _portfolios = new();
    private readonly FakeFinancialOperationRepository _operations = new();
    private readonly FakePriceObservationRepository _prices = new();

    [Fact]
    public async Task LoadAsync_ShouldBuildAccountHistoryAndCurrentTotals()
    {
        var checking = AddAccount("Conto corrente", AccountType.Checking);
        var cash = AddAccount("Cassa", AccountType.Cash);
        var card = AddAccount("Carta di credito", AccountType.Card);

        AddReading(checking, 100m, new DateOnly(2026, 1, 1));
        AddReading(checking, 150m, new DateOnly(2026, 2, 1));
        AddReading(cash, 50m, new DateOnly(2026, 2, 1));
        AddReading(card, -20m, new DateOnly(2026, 2, 1));

        var overview = await BuildService().LoadAsync();
        var accounts = overview.Accounts;

        accounts.CurrentValue.ShouldBe(new Money(180m, Euro));
        accounts.CurrentReference.ShouldBeNull();
        accounts.IsComplete.ShouldBeTrue();
        accounts.HasPartialHistory.ShouldBeTrue();
        accounts.Points.Count.ShouldBe(3);

        accounts.Points[0].Date.ShouldBe(new DateOnly(2026, 1, 1));
        accounts.Points[0].Value.ShouldBe(new Money(100m, Euro));
        accounts.Points[0].Reference.ShouldBeNull();
        accounts.Points[0].IsComplete.ShouldBeFalse();
        accounts.Points[^1].Date.ShouldBe(Today);
        accounts.Points[^1].Value.ShouldBe(new Money(180m, Euro));
        accounts.Points[^1].Reference.ShouldBeNull();
        accounts.Points[^1].IsComplete.ShouldBeTrue();
    }

    [Fact]
    public async Task Window_ShouldFilterByRangeAndComputeTheChange()
    {
        var checking = AddAccount("Conto corrente", AccountType.Checking);
        AddReading(checking, 100m, new DateOnly(2026, 1, 1));
        AddReading(checking, 180m, new DateOnly(2026, 2, 1));

        var overview = await BuildService().LoadAsync();
        var accounts = overview.Accounts;

        var max = accounts.Window(DashboardOverviewRange.Max, Today);
        max.Points.Count.ShouldBe(3);
        max.FirstValue.ShouldBe(new Money(100m, Euro));
        max.LastValue.ShouldBe(new Money(180m, Euro));
        max.Change.ShouldBe(new Money(80m, Euro));
        max.ChangePercent.ShouldBe(0.8m);

        var month = accounts.Window(DashboardOverviewRange.OneMonth, Today);
        month.Points.Count.ShouldBe(2);
        month.FirstValue.ShouldBe(new Money(180m, Euro));
        month.Change.ShouldBe(new Money(0m, Euro));
        month.ChangePercent.ShouldBe(0m);

        var day = accounts.Window(DashboardOverviewRange.OneDay, Today);
        day.Points.Count.ShouldBe(2);
        day.FirstValue.ShouldBe(new Money(180m, Euro));
        day.LastValue.ShouldBe(new Money(180m, Euro));
    }

    [Fact]
    public async Task Window_YearToDate_ShouldStartAtJanuaryFirst()
    {
        var checking = AddAccount("Conto corrente", AccountType.Checking);
        AddReading(checking, 50m, new DateOnly(2025, 12, 31));
        AddReading(checking, 100m, new DateOnly(2026, 1, 1));
        AddReading(checking, 180m, new DateOnly(2026, 2, 1));

        var overview = await BuildService().LoadAsync();
        var yearToDate = overview.Accounts.Window(DashboardOverviewRange.YearToDate, Today);

        yearToDate.Points[0].Date.ShouldBe(new DateOnly(2026, 1, 1));
        yearToDate.Points.ShouldAllBe(point => point.Date.Year == 2026);
        yearToDate.FirstValue.ShouldBe(new Money(100m, Euro));
        yearToDate.LastValue.ShouldBe(new Money(180m, Euro));
    }

    [Fact]
    public async Task LoadAsync_ShouldExposeAPartialTotalWithoutTreatingMissingAccountReadingsAsZero()
    {
        var checking = AddAccount("Conto corrente", AccountType.Checking);
        AddAccount("Cassa", AccountType.Cash);
        AddReading(checking, 100m, new DateOnly(2026, 2, 1));

        var overview = await BuildService().LoadAsync();
        var accounts = overview.Accounts;

        // Il totale è la somma dei soli conti con saldo disponibile ed è dichiarato parziale.
        accounts.CurrentValue.ShouldBe(new Money(100m, Euro));
        accounts.CurrentReference.ShouldBeNull();
        accounts.IsComplete.ShouldBeFalse();
        accounts.IncompletenessReasons.ShouldContain(reason => reason.Contains("1 conto su 2"));
        accounts.Points[^1].Value.ShouldBe(new Money(100m, Euro));
        accounts.Points[^1].IsComplete.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadAsync_ShouldComposeTheInvestmentHistoryAndCurrentPerformance()
    {
        var portfolio = new InvestmentPortfolio(Guid.NewGuid(), "ETF Core", Euro);
        _portfolios.Portfolios.Add(portfolio);
        var instrument = new Instrument(
            Guid.NewGuid(), "ETF globale", InstrumentType.Etf, Euro, PriceUnit.PerUnit);
        var account = new Account(
            Guid.NewGuid(), "Conto titoli", AccountType.Checking, Euro, AccountStatus.Active);

        AddPrice(instrument, 80m, new DateOnly(2026, 1, 1));
        AddBuy(portfolio, instrument, account, 2m, 80m, new DateOnly(2026, 1, 5));
        AddPrice(instrument, 90m, new DateOnly(2026, 1, 10));

        var overview = await BuildService(new DateOnly(2026, 1, 20)).LoadAsync();
        var investments = overview.Investments;

        investments.CurrentValue.ShouldBe(new Money(180m, Euro));
        investments.CurrentReference.ShouldBe(new Money(160m, Euro));
        investments.CurrentChange.ShouldBe(new Money(20m, Euro));
        investments.CurrentChangePercent.ShouldBe(0.125m);
        investments.IsComplete.ShouldBeTrue();
        investments.HasPartialHistory.ShouldBeFalse();
        investments.Points.Count.ShouldBe(3);
        investments.Points[0].Date.ShouldBe(new DateOnly(2026, 1, 5));
        investments.Points[0].Value.ShouldBe(new Money(160m, Euro));
        investments.Points[0].Reference.ShouldBe(new Money(160m, Euro));
        investments.Points[^1].Value.ShouldBe(new Money(180m, Euro));
        investments.Points.All(point => point.IsComplete).ShouldBeTrue();
    }

    private DashboardOverviewService BuildService(DateOnly? today = null)
    {
        var investmentService = new InvestmentOverviewService(
            _portfolios,
            _operations,
            _prices,
            () => today ?? Today);

        return new DashboardOverviewService(
            investmentService,
            _accounts,
            _manualBalances,
            () => today ?? Today);
    }

    private Account AddAccount(string name, AccountType type)
    {
        var account = new Account(
            Guid.NewGuid(), name, type, Euro, AccountStatus.Active);
        _accounts.Accounts.Add(account);
        return account;
    }

    private void AddReading(Account account, decimal amount, DateOnly validAt) =>
        _manualBalances.Readings.Add(new ManualBalance(
            Guid.NewGuid(),
            account,
            new Money(amount, account.Currency),
            validAt,
            RecordedAt));

    private void AddBuy(
        InvestmentPortfolio portfolio,
        Instrument instrument,
        Account account,
        decimal quantity,
        decimal price,
        DateOnly date) =>
        _operations.Operations.Add(InvestmentOperation.Buy(
            Guid.NewGuid(),
            portfolio,
            instrument,
            account,
            new Quantity(quantity),
            QuotedPrice.AtUnitPrice(new UnitPrice(new Money(price, instrument.QuoteCurrency))),
            null,
            date,
            RecordedAt));

    private void AddPrice(Instrument instrument, decimal price, DateOnly validAt) =>
        _prices.Observations.Add(PriceObservation.AtUnitPrice(
            Guid.NewGuid(),
            instrument,
            new UnitPrice(new Money(price, instrument.QuoteCurrency)),
            validAt,
            RecordedAt,
            "Test"));
}
