using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class MovementServiceTests
{
    private static readonly Currency Euro = Currency.Euro;

    private readonly FakeFinancialOperationRepository _operations = new();
    private readonly FakeAccountPostingRepository _postings;
    private readonly MovementService _service;

    private readonly Account _account = new(
        Guid.NewGuid(), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
    private readonly Category _category = new(Guid.NewGuid(), "Stipendio", CategoryType.Income, null);

    public MovementServiceTests()
    {
        _postings = new FakeAccountPostingRepository(_operations.Operations);
        _service = new MovementService(_postings);
    }

    [Fact]
    public async Task ListAsync_ShouldFilterByAccount()
    {
        AddIncome(_account, _category, new DateOnly(2026, 3, 1), 100m);
        var otherAccount = new Account(
            Guid.NewGuid(), "Cassa", AccountType.Cash, Euro, AccountStatus.Active);
        AddIncome(otherAccount, _category, new DateOnly(2026, 3, 2), 50m);

        var movements = await _service.ListAsync(_account.Id);

        movements.Select(posting => posting.AccountId)
            .ShouldAllBe(accountId => accountId == _account.Id);
    }

    [Fact]
    public async Task ListAsync_ShouldFilterByDateRange()
    {
        AddIncome(_account, _category, new DateOnly(2026, 3, 5), 10m);
        AddIncome(_account, _category, new DateOnly(2026, 3, 10), 20m);
        AddIncome(_account, _category, new DateOnly(2026, 3, 15), 30m);

        var movements = await _service.ListAsync(
            null, new DateOnly(2026, 3, 6), new DateOnly(2026, 3, 14));

        movements.ShouldHaveSingleItem().SignedAmount.ShouldBe(20m);
    }

    [Fact]
    public async Task ListAsync_ShouldReturnMovementsInDeterministicOrder()
    {
        var firstExpense = new Expense(
            Guid.NewGuid(), _account, _category,
            new PositiveMoney(new Money(5m, Euro)), new DateOnly(2026, 3, 1),
            new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
        var firstIncome = new Income(
            Guid.Parse("00000000-0000-0000-0000-000000000001"), _account, _category,
            new PositiveMoney(new Money(10m, Euro)), new DateOnly(2026, 3, 5),
            new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero));
        var secondIncome = new Income(
            Guid.Parse("00000000-0000-0000-0000-000000000002"), _account, _category,
            new PositiveMoney(new Money(20m, Euro)), new DateOnly(2026, 3, 5),
            new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero));
        _operations.Operations.AddRange([firstExpense, firstIncome, secondIncome]);

        var movements = (await _service.ListAsync()).ToArray();

        movements.Select(posting => SignedAmount(posting)).ShouldBe([-5m, 10m, 20m]);
        movements.Select(posting => posting.OperationDate)
            .ShouldBeInOrder(SortDirection.Ascending);
    }

    [Fact]
    public async Task ListAsync_ShouldExcludeReversedOperations()
    {
        var income = AddIncome(_account, _category, new DateOnly(2026, 3, 1), 100m);
        new ReversalOperation(
            Guid.NewGuid(), income,
            new DateOnly(2026, 3, 11), new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero));

        var movements = await _service.ListAsync();

        movements.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListAsync_ShouldFlowTheCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await _service.ListAsync(null, null, null, cts.Token);

        _postings.ReceivedTokens.ShouldContain(cts.Token);
    }

    private Income AddIncome(Account account, Category category, DateOnly date, decimal amount)
    {
        var income = new Income(
            Guid.NewGuid(), account, category,
            new PositiveMoney(new Money(amount, Euro)), date,
            new DateTimeOffset(date, new TimeOnly(12, 0), TimeSpan.Zero));
        _operations.Operations.Add(income);
        return income;
    }

    private Expense AddExpense(Account account, Category category, DateOnly date, decimal amount)
    {
        var expense = new Expense(
            Guid.NewGuid(), account, category,
            new PositiveMoney(new Money(amount, Euro)), date,
            new DateTimeOffset(date, new TimeOnly(12, 0), TimeSpan.Zero));
        _operations.Operations.Add(expense);
        return expense;
    }

    private static decimal SignedAmount(AccountPosting posting) => posting.SignedAmount;
}
