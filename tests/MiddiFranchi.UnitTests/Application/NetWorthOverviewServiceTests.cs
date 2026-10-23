using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class NetWorthOverviewServiceTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly DateOnly Today = new(2026, 3, 31);
    private static readonly DateTimeOffset RecordedAt = new(2026, 3, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeManualBalanceRepository _readings = new();
    private readonly FakeFinancialOperationRepository _operations = new();
    private readonly FakePriceObservationRepository _prices = new();

    [Fact]
    public async Task SummarizeAsync_ShouldUseManualBalancesAndEurInvestmentValues()
    {
        var account = AddAccount("Conto corrente");
        AddReading(account, 100m, new DateOnly(2026, 3, 20));
        var instrument = AddInstrument("ETF globale", Euro);
        AddBuy(instrument, account, 2m, 50m, new DateOnly(2026, 3, 10));
        AddPrice(instrument, 60m, new DateOnly(2026, 3, 20));

        var summary = await BuildService().SummarizeAsync(Today);

        summary.IsComplete.ShouldBeTrue();
        summary.AccountsSubtotal.ShouldBe(new Money(100m, Euro));
        summary.InvestmentsSubtotal.ShouldBe(new Money(120m, Euro));
        summary.TotalValue.ShouldBe(new Money(220m, Euro));
    }

    [Fact]
    public async Task SummarizeAsync_ShouldNotTreatAMissingReadingAsZero()
    {
        AddAccount("Conto senza rilevazione");

        var summary = await BuildService().SummarizeAsync(Today);

        summary.IsComplete.ShouldBeFalse();
        summary.AccountsSubtotal.ShouldBeNull();
        summary.TotalValue.ShouldBeNull();
        summary.IncompletenessReasons.ShouldContain(reason => reason.Contains("rilevazione manuale"));
    }

    [Fact]
    public async Task SummarizeAsync_ShouldNotTreatAnUnvaluedPositionAsZero()
    {
        var account = AddAccount("Conto titoli");
        AddReading(account, 100m, new DateOnly(2026, 3, 20));
        var instrument = AddInstrument("ETF globale", Euro);
        AddBuy(instrument, account, 2m, 50m, new DateOnly(2026, 3, 10));

        var summary = await BuildService().SummarizeAsync(Today);

        summary.IsComplete.ShouldBeFalse();
        summary.AccountsSubtotal.ShouldBe(new Money(100m, Euro));
        summary.InvestmentsSubtotal.ShouldBeNull();
        summary.TotalValue.ShouldBeNull();
        summary.IncompletenessReasons.ShouldContain(reason => reason.Contains("quotazione"));
    }

    private NetWorthOverviewService BuildService() => new(
        _accounts,
        _readings,
        _operations,
        _prices);

    private Account AddAccount(string name)
    {
        var account = new Account(
            Guid.NewGuid(), name, AccountType.Checking, Euro, AccountStatus.Active);
        _accounts.Accounts.Add(account);
        return account;
    }

    private void AddReading(Account account, decimal amount, DateOnly validAt) =>
        _readings.Readings.Add(new ManualBalance(
            Guid.NewGuid(),
            account,
            new Money(amount, Euro),
            validAt,
            RecordedAt));

    private static Instrument AddInstrument(string name, Currency currency) =>
        new(Guid.NewGuid(), name, AssetClass.Equity, currency, PriceUnit.PerUnit);

    private void AddBuy(
        Instrument instrument,
        Account account,
        decimal quantity,
        decimal price,
        DateOnly date) =>
        _operations.Operations.Add(InvestmentOperation.Buy(
            Guid.NewGuid(),
            instrument,
            account,
            new Quantity(quantity),
            QuotedPrice.AtUnitPrice(new UnitPrice(new Money(price, Euro))),
            null,
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
