using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class ManualBalanceServiceTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly DateOnly Today = new(2026, 10, 20);
    private static readonly DateOnly ValidAt = new(2026, 4, 1);

    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeManualBalanceRepository _manualBalances = new();

    private ManualBalanceService CreateService() =>
        new(_accounts, _manualBalances, () => Today);

    private ManualBalanceService _service = null!;

    public ManualBalanceServiceTests()
    {
        _service = CreateService();
    }

    private Account RegisteredAccount(string name = "Conto corrente")
    {
        var account = new Account(Guid.NewGuid(), name, AccountType.Checking, Euro, AccountStatus.Active);
        _accounts.Accounts.Add(account);
        return account;
    }

    [Fact]
    public async Task RecordAsync_ShouldPersistANewReading()
    {
        var account = RegisteredAccount();

        var reading = await _service.RecordAsync(
            new NewManualBalance(account.Id, new Money(1234.56789m, Euro), ValidAt));

        reading.Account.ShouldBe(account);
        reading.Amount.ShouldBe(new Money(1234.56789m, Euro));
        reading.ValidAt.ShouldBe(ValidAt);
        reading.RecordedAt.ShouldNotBe(default(DateTimeOffset));
        reading.Id.ShouldNotBe(Guid.Empty);
        _manualBalances.Readings.ShouldHaveSingleItem().ShouldBe(reading);
    }

    [Fact]
    public async Task RecordAsync_ShouldRejectAMissingAccount()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.RecordAsync(new NewManualBalance(Guid.NewGuid(), new Money(10m, Euro), ValidAt)));

        exception.Message.ShouldContain("Il conto");
    }

    [Fact]
    public async Task RecordAsync_ShouldAcceptNegativeAndZeroBalances()
    {
        var account = RegisteredAccount();

        var negative = await _service.RecordAsync(
            new NewManualBalance(account.Id, new Money(-40m, Euro), ValidAt));
        var zero = await _service.RecordAsync(
            new NewManualBalance(account.Id, new Money(0m, Euro), ValidAt));

        negative.Amount.Amount.ShouldBe(-40m);
        zero.Amount.Amount.ShouldBe(0m);
    }

    [Fact]
    public async Task GetCurrentBalanceAsync_ShouldReturnTheLatestRegisteredReading()
    {
        var account = RegisteredAccount();
        var first = await RecordAsync(account, 100m, new(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), ValidAt);
        var second = await RecordAsync(account, 80m, new(2026, 4, 2, 9, 0, 0, TimeSpan.Zero), ValidAt);

        var current = await _service.GetCurrentBalanceAsync(account.Id);

        current.ShouldBe(second);
        _manualBalances.Readings.ShouldBe([first, second]);
    }

    [Fact]
    public async Task GetCurrentBalanceAsync_ShouldReturnNullWhenNoReadingExists()
    {
        var account = RegisteredAccount();

        var current = await _service.GetCurrentBalanceAsync(account.Id);

        current.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrentBalanceAsync_ShouldRejectAMissingAccount()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.GetCurrentBalanceAsync(Guid.NewGuid()));

        exception.Message.ShouldContain("Il conto");
    }

    [Fact]
    public async Task GetBalanceOnOrBeforeAsync_ShouldReturnNullBeforeTheFirstReading()
    {
        var account = RegisteredAccount();
        await RecordAsync(account, 100m, new(2026, 4, 10, 9, 0, 0, TimeSpan.Zero), new DateOnly(2026, 4, 10));

        var balance = await _service.GetBalanceOnOrBeforeAsync(account.Id, new DateOnly(2026, 4, 9));

        balance.ShouldBeNull();
    }

    [Fact]
    public async Task ListForAccountAsync_ShouldReturnTheHistoryInDeterministicOrder()
    {
        var account = RegisteredAccount();
        var older = await RecordAsync(account, 100m, new(2026, 4, 10, 9, 0, 0, TimeSpan.Zero), new DateOnly(2026, 3, 31));
        var newer = await RecordAsync(account, 80m, new(2026, 4, 11, 9, 0, 0, TimeSpan.Zero), new DateOnly(2026, 4, 5));

        var history = await _service.ListForAccountAsync(account.Id);

        history.ShouldBe([older, newer]);
    }

    [Fact]
    public async Task UpdateAsync_ShouldChangeAmountAndDateKeepingTheRegistration()
    {
        var account = RegisteredAccount();
        var reading = await RecordAsync(
            account, 100m, new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), ValidAt);

        var updated = await _service.UpdateAsync(
            reading.Id, new Money(150m, Euro), new DateOnly(2026, 4, 2));

        updated.Id.ShouldBe(reading.Id);
        updated.Amount.ShouldBe(new Money(150m, Euro));
        updated.ValidAt.ShouldBe(new DateOnly(2026, 4, 2));
        updated.RecordedAt.ShouldBe(reading.RecordedAt);
        _manualBalances.Readings.ShouldHaveSingleItem().ShouldBe(updated);
    }

    [Fact]
    public async Task UpdateAsync_ShouldRejectAFutureEconomicDate()
    {
        var account = RegisteredAccount();
        var reading = await RecordAsync(
            account, 100m, new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), ValidAt);

        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.UpdateAsync(reading.Id, new Money(150m, Euro), Today.AddDays(1)));

        exception.Message.ShouldContain("successiva alla data corrente");
        _manualBalances.Readings.ShouldHaveSingleItem().Amount.ShouldBe(new Money(100m, Euro));
    }

    [Fact]
    public async Task UpdateAsync_ShouldRejectAMissingReading()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.UpdateAsync(Guid.NewGuid(), new Money(10m, Euro), ValidAt));

        exception.Message.ShouldContain("non esiste");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveTheReading()
    {
        var account = RegisteredAccount();
        var reading = await RecordAsync(
            account, 100m, new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), ValidAt);

        await _service.DeleteAsync(reading.Id);

        _manualBalances.Readings.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRejectAMissingReading()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.DeleteAsync(Guid.NewGuid()));

        exception.Message.ShouldContain("non esiste");
    }

    [Fact]
    public async Task Operations_ShouldNotAlterTheManualBalance()
    {
        var account = RegisteredAccount();
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income, null);
        var expenseCategory = new Category(Guid.NewGuid(), "Spese", CategoryType.Expense, null);
        _accounts.Accounts.Add(account);
        var categories = new FakeCategoryRepository();
        categories.Categories.Add(category);
        categories.Categories.Add(expenseCategory);
        var operations = new FakeFinancialOperationRepository();
        var registration = new OperationRegistrationService(_accounts, categories, operations);
        await _service.RecordAsync(new NewManualBalance(account.Id, new Money(500m, Euro), ValidAt));

        await registration.IncomeAsync(new RegisterIncomeRequest(
            account.Id, category.Id, new PositiveMoney(new Money(100m, Euro)), new DateOnly(2026, 4, 15)));
        await registration.ExpenseAsync(new RegisterExpenseRequest(
            account.Id, expenseCategory.Id, new PositiveMoney(new Money(30m, Euro)), new DateOnly(2026, 4, 16)));

        var current = await _service.GetCurrentBalanceAsync(account.Id);

        current.ShouldNotBeNull();
        current.Amount.ShouldBe(new Money(500m, Euro));
        operations.Operations.Count.ShouldBe(2);
        _manualBalances.Readings.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RecordAsync_ShouldFlowTheCancellationToken()
    {
        var account = RegisteredAccount();
        using var cts = new CancellationTokenSource();

        await _service.RecordAsync(new NewManualBalance(account.Id, new Money(10m, Euro), ValidAt), cts.Token);

        _manualBalances.ReceivedTokens.ShouldContain(cts.Token);
    }

    [Fact]
    public async Task RecordAsync_ShouldAcceptAReadingWithTodaysData()
    {
        var account = RegisteredAccount();

        var reading = await _service.RecordAsync(
            new NewManualBalance(account.Id, new Money(100m, Euro), Today));

        reading.ValidAt.ShouldBe(Today);
        _manualBalances.Readings.ShouldHaveSingleItem().ShouldBe(reading);
    }

    [Fact]
    public async Task RecordAsync_ShouldRejectAFutureEconomicDate()
    {
        var account = RegisteredAccount();
        var futureDate = Today.AddDays(1);

        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.RecordAsync(new NewManualBalance(account.Id, new Money(10m, Euro), futureDate)));

        exception.Message.ShouldContain("successiva alla data corrente");
        exception.Message.ShouldContain(FormatDate(Today));
        exception.Message.ShouldContain(FormatDate(futureDate));
        _manualBalances.Readings.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordAsync_ShouldAcceptTheRequestedDateAndRejectOnlyTheNext()
    {
        var account = RegisteredAccount();
        var lastValidDate = Today;

        var latestAccepted = await _service.RecordAsync(
            new NewManualBalance(account.Id, new Money(10m, Euro), lastValidDate));
        var firstRejected = await Should.ThrowAsync<ArgumentException>(
            () => _service.RecordAsync(new NewManualBalance(account.Id, new Money(11m, Euro), lastValidDate.AddDays(1))));

        latestAccepted.ValidAt.ShouldBe(Today);
        firstRejected.Message.ShouldContain("successiva alla data corrente");
        _manualBalances.Readings.ShouldHaveSingleItem();
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private Task<ManualBalance> RecordAsync(
        Account account,
        decimal amount,
        DateTimeOffset recordedAt,
        DateOnly validAt)
    {
        var reading = new ManualBalance(
            Guid.NewGuid(), account, new Money(amount, Euro), validAt, recordedAt);
        _manualBalances.Readings.Add(reading);
        return Task.FromResult(reading);
    }
}
