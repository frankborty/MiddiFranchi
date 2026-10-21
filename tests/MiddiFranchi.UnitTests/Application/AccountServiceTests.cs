using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class AccountServiceTests
{
    private static readonly Currency Euro = Currency.Euro;

    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeFinancialOperationRepository _operations = new();
    private readonly FakeAccountPostingRepository _postings;

    private readonly AccountService _service;

    public AccountServiceTests()
    {
        _postings = new FakeAccountPostingRepository(_operations.Operations);
        _service = new AccountService(_accounts, _postings);
    }

    [Fact]
    public async Task Create_ShouldPersistANewActiveAccount()
    {
        var request = new NewAccount("Conto corrente", AccountType.Checking);

        var created = await _service.CreateAsync(request);

        created.Name.ShouldBe("Conto corrente");
        created.Type.ShouldBe(AccountType.Checking);
        created.Currency.ShouldBe(Euro);
        created.Status.ShouldBe(AccountStatus.Active);
        _accounts.Accounts.ShouldHaveSingleItem().ShouldBe(created);
        created.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Create_ShouldRejectAnEmptyName()
    {
        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.CreateAsync(new NewAccount(" ", AccountType.Checking)));

        exception.Message.ShouldContain("Il nome del conto è obbligatorio");
    }

    [Fact]
    public async Task GetById_ShouldReturnThePersistedAccount()
    {
        var created = await CreateAccountAsync("Conto corrente");

        var loaded = await _service.GetByIdAsync(created.Id);

        loaded.ShouldNotBeNull();
        loaded.Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task GetById_ShouldReturnNullForAMissingAccount()
    {
        var loaded = await _service.GetByIdAsync(Guid.NewGuid());

        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task List_ShouldReturnAccountsInDeterministicOrder()
    {
        await _service.CreateAsync(new NewAccount("Conto corrente", AccountType.Checking));
        await _service.CreateAsync(new NewAccount("Banca orale", AccountType.Cash));

        var listed = await _service.ListAsync();

        listed.Select(account => account.Name).ShouldBe(["Banca orale", "Conto corrente"]);
    }

    [Fact]
    public async Task GetBalance_ShouldSumOnlyActivePostingsUntilTheAsOfDate()
    {
        var account = await CreateAccountAsync("Conto corrente");
        var category = new Category(Guid.NewGuid(), "Categoria", CategoryType.Income, null);
        var income = new Income(
            Guid.NewGuid(), account, category,
            new PositiveMoney(new Money(100m, Euro)),
            new DateOnly(2026, 3, 1), new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
        var expense = new Expense(
            Guid.NewGuid(), account, category,
            new PositiveMoney(new Money(40m, Euro)),
            new DateOnly(2026, 3, 8), new DateTimeOffset(2026, 3, 8, 12, 0, 0, TimeSpan.Zero));
        _operations.Operations.AddRange([income, expense]);

        var balance = await _service.GetCalculatedBalanceAsync(account.Id, new DateOnly(2026, 3, 10));

        balance.ShouldBe(new Money(60m, Euro));
    }

    [Fact]
    public async Task GetBalance_ShouldExcludeReversedOperations()
    {
        var account = await CreateAccountAsync("Conto corrente");
        var category = new Category(Guid.NewGuid(), "Categoria", CategoryType.Income, null);
        var income = new Income(
            Guid.NewGuid(), account, category,
            new PositiveMoney(new Money(100m, Euro)),
            new DateOnly(2026, 3, 1), new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
        var reversal = new ReversalOperation(
            Guid.NewGuid(), income,
            new DateOnly(2026, 3, 11), new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero));
        _operations.Operations.AddRange([income, reversal]);

        var balance = await _service.GetCalculatedBalanceAsync(account.Id, new DateOnly(2026, 4, 1));

        balance.ShouldBe(new Money(0m, Euro));
    }

    [Fact]
    public async Task GetBalance_ShouldThrowForAMissingAccount()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.GetCalculatedBalanceAsync(Guid.NewGuid()));

        exception.Message.ShouldContain("non esiste");
    }

    [Fact]
    public async Task GetBalance_ShouldFlowTheCancellationToken()
    {
        var account = await CreateAccountAsync("Conto corrente");
        using var cts = new CancellationTokenSource();

        await _service.GetCalculatedBalanceAsync(account.Id, null, cts.Token);

        _postings.ReceivedTokens.ShouldContain(cts.Token);
    }

    private Task<Account> CreateAccountAsync(string name) =>
        _service.CreateAsync(new NewAccount(name, AccountType.Checking));
}
