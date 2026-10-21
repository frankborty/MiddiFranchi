using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.Persistence.Repositories;
using MiddiFranchi.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class AccountPostingMovementTests : PostgresTestBase
{
    public AccountPostingMovementTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task ListAsync_ShouldFilterByAccountAndDateRangeInDeterministicOrder()
    {
        var account = await AddAccountAsync("Conto corrente");
        var otherAccount = await AddAccountAsync("Cassa");
        var incomeCategory = await AddCategoryAsync(CategoryType.Income);
        var expenseCategory = await AddCategoryAsync(CategoryType.Expense);
        var operations = new FinancialOperationRepository(Context);
        await operations.AddAsync(Income(account, incomeCategory, new DateOnly(2026, 3, 5), 10m));
        await operations.AddAsync(IncomeWithId(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            account, incomeCategory, new DateOnly(2026, 3, 10), 20m));
        await operations.AddAsync(Expense(account, expenseCategory, new DateOnly(2026, 3, 15), 5m));
        await operations.AddAsync(IncomeWithId(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            otherAccount, incomeCategory, new DateOnly(2026, 3, 10), 50m));
        ClearTracker();

        var repository = new AccountPostingRepository(Context);
        var allMovements = await repository.ListAsync(null, null, null);

        allMovements
            .First(posting => posting.OperationDate == new DateOnly(2026, 3, 5))
            .SignedAmount.ShouldBe(10m);

        var accountMovements = await repository.ListAsync(account.Id, null, null);

        accountMovements.Count.ShouldBe(3);
        accountMovements
            .Select(posting => posting.AccountId)
            .ShouldAllBe(accountId => accountId == account.Id);

        var rangedMovements = await repository.ListAsync(
            null, new DateOnly(2026, 3, 6), new DateOnly(2026, 3, 14));

        rangedMovements
            .Select(posting => posting.SignedAmount)
            .ShouldBe([20m, 50m]);
    }

    [PostgresFact]
    public async Task ListAsync_ShouldOrderMovementsByDateThenOperationAndPosting()
    {
        var account = await AddAccountAsync("Conto corrente");
        var incomeCategory = await AddCategoryAsync(CategoryType.Income);
        var storedWithLargerId = IncomeWithId(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            account, incomeCategory, new DateOnly(2026, 3, 8), 30m);
        var storedWithSmallerId = IncomeWithId(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            account, incomeCategory, new DateOnly(2026, 3, 8), 10m);
        var operations = new FinancialOperationRepository(Context);
        await operations.AddAsync(storedWithLargerId);
        await operations.AddAsync(storedWithSmallerId);
        ClearTracker();

        var movements = await new AccountPostingRepository(Context).ListAsync(account.Id, null, null);

        movements
            .Select(posting => posting.Operation.Id)
            .ShouldBe([storedWithSmallerId.Id, storedWithLargerId.Id]);
    }

    [PostgresFact]
    public async Task Movements_ShouldExcludeReversedOperationsAndKeepTheOriginalRow()
    {
        var (account, category) = await AddOrdinaryContextAsync(CategoryType.Income);
        var registration = new OperationRegistrationService(
            new AccountRepository(Context),
            new CategoryRepository(Context),
            new FinancialOperationRepository(Context));
        var income = await registration.IncomeAsync(new RegisterIncomeRequest(
            account.Id,
            category.Id,
            new PositiveMoney(new Money(100m, Currency.Euro)),
            new DateOnly(2026, 3, 1)));
        ClearTracker();

        var operations = new FinancialOperationRepository(Context);
        var original = (await operations.GetByIdAsync(income.Id)).ShouldBeOfType<Income>();
        original.Status.ShouldBe(OperationStatus.Registered);
        await operations.AddAsync(new ReversalOperation(
            Guid.NewGuid(),
            original,
            new DateOnly(2026, 3, 11),
            new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero)));
        ClearTracker();

        var postingRepository = new AccountPostingRepository(Context);
        var movements = await postingRepository.ListAsync(
            account.Id, fromDate: null, toDate: new DateOnly(2026, 4, 1));

        movements.ShouldBeEmpty();
        (await Context.AccountPostings.CountAsync(posting => posting.AccountId == account.Id))
            .ShouldBe(1);

        var accountService = new AccountService(
            new AccountRepository(Context),
            postingRepository,
            new FinancialOperationRepository(Context),
            new LoanRepository(Context));
        var balance = await accountService.GetCalculatedBalanceAsync(account.Id, new DateOnly(2026, 4, 1));

        balance.ShouldBe(new Money(0m, Currency.Euro));
    }

    [PostgresFact]
    public async Task AccountService_ShouldComputeTheBalanceFromActivePostings()
    {
        var (account, incomeCategory) = await AddOrdinaryContextAsync(CategoryType.Income);
        var expenseCategory = await AddCategoryAsync(CategoryType.Expense);
        var registration = new OperationRegistrationService(
            new AccountRepository(Context),
            new CategoryRepository(Context),
            new FinancialOperationRepository(Context));
        await registration.IncomeAsync(new RegisterIncomeRequest(
            account.Id,
            incomeCategory.Id,
            new PositiveMoney(new Money(150m, Currency.Euro)),
            new DateOnly(2026, 3, 1)));
        await registration.ExpenseAsync(new RegisterExpenseRequest(
            account.Id,
            expenseCategory.Id,
            new PositiveMoney(new Money(70m, Currency.Euro)),
            new DateOnly(2026, 3, 8)));
        ClearTracker();

        var accountService = new AccountService(
            new AccountRepository(Context),
            new AccountPostingRepository(Context),
            new FinancialOperationRepository(Context),
            new LoanRepository(Context));
        var balance = await accountService.GetCalculatedBalanceAsync(account.Id, new DateOnly(2026, 3, 10));

        balance.ShouldBe(new Money(80m, Currency.Euro));
    }

    [PostgresFact]
    public async Task TransferViaRegistrationService_ShouldPersistBothLegs()
    {
        var accountRepository = new AccountRepository(Context);
        var source = new Account(
            Guid.NewGuid(), "Conto EUR", AccountType.Checking,
            Currency.Euro, AccountStatus.Active);
        var destination = new Account(
            Guid.NewGuid(), "Conto USD", AccountType.Checking,
            Currency.Euro, AccountStatus.Active);
        await accountRepository.AddAsync(source);
        await accountRepository.AddAsync(destination);

        var registration = new OperationRegistrationService(
            accountRepository,
            new CategoryRepository(Context),
            new FinancialOperationRepository(Context));
        var transfer = await registration.TransferAsync(new RegisterTransferRequest(
            source.Id,
            destination.Id,
            new PositiveMoney(new Money(100m, Currency.Euro)),
            new PositiveMoney(new Money(100m, Currency.Euro)),
            new DateOnly(2026, 3, 12)));
        ClearTracker();

        var loaded = (await new FinancialOperationRepository(Context)
            .GetByIdAsync(transfer.Id)).ShouldBeOfType<Transfer>();
        loaded.SourceAmount.Value.Amount.ShouldBe(100m);
        loaded.DestinationAmount.Value.Amount.ShouldBe(100m);

        var postingRepository = new AccountPostingRepository(Context);
        var sourceMovements = await postingRepository.ListAsync(source.Id, null, null);
        var destinationMovements = await postingRepository.ListAsync(destination.Id, null, null);

        sourceMovements.ShouldHaveSingleItem().SignedAmount.ShouldBe(-100m);
        destinationMovements.ShouldHaveSingleItem().SignedAmount.ShouldBe(100m);
        AccountBalanceCalculator
            .Balance(source, sourceMovements, new DateOnly(2026, 3, 12))
            .ShouldBe(new Money(-100m, Currency.Euro));
        AccountBalanceCalculator
            .Balance(destination, destinationMovements, new DateOnly(2026, 3, 12))
            .ShouldBe(new Money(100m, Currency.Euro));
    }

    [PostgresFact]
    public async Task Registration_ShouldRejectAnInexistentCategory()
    {
        var accountRepository = new AccountRepository(Context);
        var account = new Account(
            Guid.NewGuid(), "Conto corrente", AccountType.Checking,
            Currency.Euro, AccountStatus.Active);
        await accountRepository.AddAsync(account);
        var registration = new OperationRegistrationService(
            accountRepository,
            new CategoryRepository(Context),
            new FinancialOperationRepository(Context));

        var missingCategory = await Should.ThrowAsync<InvalidOperationException>(
            () => registration.IncomeAsync(new RegisterIncomeRequest(
                account.Id,
                Guid.NewGuid(),
                new PositiveMoney(new Money(10m, Currency.Euro)),
                new DateOnly(2026, 3, 10))));
        missingCategory.Message.ShouldContain("La categoria");
    }

    private async Task<Account> AddAccountAsync(string name)
    {
        var account = new Account(
            Guid.NewGuid(), name, AccountType.Checking, Currency.Euro, AccountStatus.Active);
        await new AccountRepository(Context).AddAsync(account);
        return account;
    }

    private async Task<Category> AddCategoryAsync(CategoryType type)
    {
        var category = new Category(Guid.NewGuid(), "Categoria", type);
        await new CategoryRepository(Context).AddAsync(category);
        return category;
    }

    private async Task<(Account Account, Category Category)> AddOrdinaryContextAsync(CategoryType type)
    {
        var account = await AddAccountAsync("Conto");
        var category = await AddCategoryAsync(type);
        return (account, category);
    }

    private static Income Income(Account account, Category category, DateOnly date, decimal amount) =>
        new(
            Guid.NewGuid(), account, category,
            new PositiveMoney(new Money(amount, Currency.Euro)), date,
            new DateTimeOffset(date, new TimeOnly(12, 0), TimeSpan.Zero));

    private static Income IncomeWithId(
        Guid id, Account account, Category category, DateOnly date, decimal amount) =>
        new(
            id, account, category,
            new PositiveMoney(new Money(amount, Currency.Euro)), date,
            new DateTimeOffset(date, new TimeOnly(12, 0), TimeSpan.Zero));

    private static Expense Expense(Account account, Category category, DateOnly date, decimal amount) =>
        new(
            Guid.NewGuid(), account, category,
            new PositiveMoney(new Money(amount, Currency.Euro)), date,
            new DateTimeOffset(date, new TimeOnly(12, 0), TimeSpan.Zero));
}
