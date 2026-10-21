using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class AccountOverviewServiceTests
{
    private static readonly Currency Euro = Currency.Euro;

    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeCategoryRepository _categories = new();
    private readonly FakeFinancialOperationRepository _operations = new();
    private readonly FakeManualBalanceRepository _manualBalances = new();
    private readonly AccountOverviewService _service;

    public AccountOverviewServiceTests()
    {
        _service = new AccountOverviewService(
            _accounts,
            _manualBalances,
            new FakeAccountPostingRepository(_operations.Operations));
    }

    private Account RegisteredAccount(string name)
    {
        var account = new Account(Guid.NewGuid(), name, AccountType.Checking, Euro, AccountStatus.Active);
        _accounts.Accounts.Add(account);
        return account;
    }

    private void AddReading(Account account, decimal amount, DateTimeOffset recordedAt, DateOnly validAt) =>
        _manualBalances.Readings.Add(new ManualBalance(
            Guid.NewGuid(), account, new Money(amount, Euro), validAt, recordedAt));

    private void AddIncome(Account account, DateOnly date, decimal amount)
    {
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income, null);
        _categories.Categories.Add(category);
        _operations.Operations.Add(new Income(
            Guid.NewGuid(), account, category,
            new PositiveMoney(new Money(amount, Euro)), date,
            new DateTimeOffset(date, new TimeOnly(12, 0), TimeSpan.Zero)));
    }

    [Fact]
    public async Task ListAsync_ShouldFlagAnAccountWithoutManualReadingsAsNotAvailable()
    {
        var account = RegisteredAccount("Conto corrente");
        AddIncome(account, new DateOnly(2026, 4, 10), 40m);

        var rows = await _service.ListAsync();

        var row = rows.ShouldHaveSingleItem();
        row.ManualBalance.ShouldBeNull();
        row.ManualReading.ShouldBeNull();
        row.CalculatedBalance.ShouldBe(new Money(40m, Euro));
    }

    [Fact]
    public async Task ListAsync_ShouldUseTheLatestManualReadingAsThePrimaryBalance()
    {
        var account = RegisteredAccount("Conto corrente");
        AddReading(account, 100m, new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), new DateOnly(2026, 4, 1));
        AddReading(account, 80m, new DateTimeOffset(2026, 4, 5, 9, 0, 0, TimeSpan.Zero), new DateOnly(2026, 4, 5));
        AddIncome(account, new DateOnly(2026, 4, 10), 40m);

        var rows = await _service.ListAsync();

        var row = rows.ShouldHaveSingleItem();
        row.ManualBalance.ShouldBe(new Money(80m, Euro));
        row.CalculatedBalance.ShouldBe(new Money(40m, Euro));
        row.ManualBalance!.Value.Amount.ShouldNotBe(row.CalculatedBalance!.Value.Amount);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNullForAMissingAccount()
    {
        (await _service.GetAsync(Guid.NewGuid())).ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_ShouldNotMutateTheReadingHistory()
    {
        var account = RegisteredAccount("Conto corrente");
        AddReading(account, 100m, new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), new DateOnly(2026, 4, 1));

        await _service.ListAsync();
        await _service.ListAsync();

        _manualBalances.Readings.Count.ShouldBe(1);
        _operations.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListAsync_ShouldFollowTheDeterministicAccountOrder()
    {
        RegisteredAccount("Conto corrente");
        RegisteredAccount("Banca orale");

        var rows = await _service.ListAsync();

        rows.Select(row => row.Name).ShouldBe(["Banca orale", "Conto corrente"]);
    }
}
