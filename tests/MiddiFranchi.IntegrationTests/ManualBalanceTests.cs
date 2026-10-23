using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.Persistence.Entities;
using MiddiFranchi.Infrastructure.Persistence.Repositories;
using MiddiFranchi.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class ManualBalanceTests : PostgresTestBase
{
    public ManualBalanceTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task Reading_ShouldRoundTripAmountCurrencyDatesAndPrecision()
    {
        var account = await AddAccountAsync("Conto corrente");
        var repository = new ManualBalanceRepository(Context);
        var reading = new ManualBalance(
            Guid.NewGuid(),
            account,
            new Money(1234.5678901234567890m, Currency.Euro),
            new DateOnly(2026, 4, 1),
            new DateTimeOffset(2026, 4, 1, 18, 30, 0, TimeSpan.Zero));

        await repository.AddAsync(reading);
        ClearTracker();

        var loaded = (await repository.GetLatestAsync(account.Id)).ShouldNotBeNull();

        loaded.Id.ShouldBe(reading.Id);
        loaded.Amount.ShouldBe(reading.Amount);
        loaded.ValidAt.ShouldBe(reading.ValidAt);
        loaded.RecordedAt.ShouldBe(reading.RecordedAt);
        loaded.Account.Id.ShouldBe(account.Id);
    }

    [PostgresFact]
    public async Task ASecondReading_ShouldBecomeCurrentAndKeepThePreviousOne()
    {
        var account = await AddAccountAsync("Conto corrente");
        var service = BuildService();
        var first = await service.RecordAsync(new NewManualBalance(
            account.Id, new Money(100m, Currency.Euro), new DateOnly(2026, 4, 1)));
        var second = await service.RecordAsync(new NewManualBalance(
            account.Id, new Money(80m, Currency.Euro), new DateOnly(2026, 4, 5)));
        ClearTracker();

        var current = (await service.GetCurrentBalanceAsync(account.Id)).ShouldNotBeNull();
        var history = await service.ListForAccountAsync(account.Id);

        current.Id.ShouldBe(second.Id);
        current.Amount.ShouldBe(new Money(80m, Currency.Euro));
        history.Select(reading => reading.Id).ShouldBe([first.Id, second.Id]);
        first.Amount.ShouldBe(new Money(100m, Currency.Euro));
    }

    [PostgresFact]
    public async Task Readings_ShouldKeepTheHistoryConsultableByDate()
    {
        var account = await AddAccountAsync("Conto corrente");
        var service = BuildService();
        var march = await service.RecordAsync(new NewManualBalance(
            account.Id, new Money(100m, Currency.Euro), new DateOnly(2026, 3, 31)));
        var april = await service.RecordAsync(new NewManualBalance(
            account.Id, new Money(150m, Currency.Euro), new DateOnly(2026, 4, 30)));
        ClearTracker();

        (await service.GetBalanceOnOrBeforeAsync(account.Id, new DateOnly(2026, 3, 31)))
            .ShouldNotBeNull().Id.ShouldBe(march.Id);
        (await service.GetBalanceOnOrBeforeAsync(account.Id, new DateOnly(2026, 4, 15)))
            .ShouldNotBeNull().Id.ShouldBe(march.Id);
        (await service.GetBalanceOnOrBeforeAsync(account.Id, new DateOnly(2026, 4, 30)))
            .ShouldNotBeNull().Id.ShouldBe(april.Id);
        (await service.GetBalanceOnOrBeforeAsync(account.Id, new DateOnly(2026, 3, 1))).ShouldBeNull();
    }

    [PostgresFact]
    public async Task SameDayReadings_ShouldResolveToTheLatestRegistrationThenIdentifier()
    {
        var account = await AddAccountAsync("Conto corrente");
        var repository = new ManualBalanceRepository(Context);
        var recordedAt = new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero);
        var morning = new ManualBalance(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            account, new Money(200m, Currency.Euro), new DateOnly(2026, 4, 5),
            recordedAt);
        var evening = new ManualBalance(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            account, new Money(300m, Currency.Euro), new DateOnly(2026, 4, 5),
            recordedAt);

        await repository.AddAsync(morning);
        await repository.AddAsync(evening);
        ClearTracker();

        var latest = (await repository.GetLatestOnOrBeforeAsync(account.Id, new DateOnly(2026, 4, 5)))
            .ShouldNotBeNull();

        latest.Id.ShouldBe(evening.Id);
    }

    [PostgresFact]
    public async Task Reading_ShouldBeUpdatableAndDeletable()
    {
        var account = await AddAccountAsync("Conto corrente");
        var repository = new ManualBalanceRepository(Context);
        var service = BuildService();
        var reading = await service.RecordAsync(new NewManualBalance(
            account.Id, new Money(100m, Currency.Euro), new DateOnly(2026, 4, 1)));
        ClearTracker();

        var updated = await service.UpdateAsync(
            reading.Id, new Money(125m, Currency.Euro), new DateOnly(2026, 4, 2));
        ClearTracker();

        updated.RecordedAt.ShouldBe(reading.RecordedAt, TimeSpan.FromMilliseconds(1));
        var loaded = (await repository.GetByIdAsync(reading.Id)).ShouldNotBeNull();
        loaded.Amount.ShouldBe(new Money(125m, Currency.Euro));
        loaded.ValidAt.ShouldBe(new DateOnly(2026, 4, 2));

        await service.DeleteAsync(reading.Id);
        ClearTracker();

        (await repository.GetByIdAsync(reading.Id)).ShouldBeNull();
        (await Context.ManualBalances.CountAsync()).ShouldBe(0);
    }

    [PostgresFact]
    public async Task DeletingAnAccount_ShouldRemoveItsManualReadings()
    {
        var account = await AddAccountAsync("Conto corrente");
        await BuildService().RecordAsync(new NewManualBalance(
            account.Id, new Money(100m, Currency.Euro), new DateOnly(2026, 4, 1)));
        ClearTracker();

        var service = new AccountService(
            new AccountRepository(Context),
            new AccountPostingRepository(Context),
            new FinancialOperationRepository(Context),
            new LoanRepository(Context));
        await service.DeleteAsync(account.Id);
        ClearTracker();

        (await Context.Accounts.AnyAsync(record => record.Id == account.Id)).ShouldBeFalse();
        (await Context.ManualBalances.AnyAsync(record => record.AccountId == account.Id)).ShouldBeFalse();
    }

    [PostgresFact]
    public async Task DeletingAnAccountWithTransfers_ShouldBeRejected()
    {
        var (account, _) = await AddOrdinaryContextAsync(CategoryType.Income);
        var destination = await AddAccountAsync("Conto destinazione");
        var registration = new OperationRegistrationService(
            new AccountRepository(Context),
            new CategoryRepository(Context),
            new FinancialOperationRepository(Context));
        await registration.TransferAsync(new RegisterTransferRequest(
            account.Id,
            destination.Id,
            new PositiveMoney(new Money(50m, Currency.Euro)),
            new PositiveMoney(new Money(50m, Currency.Euro)),
            new DateOnly(2026, 4, 10)));
        ClearTracker();

        var service = new AccountService(
            new AccountRepository(Context),
            new AccountPostingRepository(Context),
            new FinancialOperationRepository(Context),
            new LoanRepository(Context));
        var error = await Should.ThrowAsync<InvalidOperationException>(
            () => service.DeleteAsync(account.Id));

        error.Message.ShouldContain("operazioni registrate");
        (await Context.Accounts.AnyAsync(record => record.Id == account.Id)).ShouldBeTrue();
    }

    [PostgresFact]
    public async Task Reading_ShouldRejectAMissingAccount()
    {
        var service = BuildService();

        var missingAccount = await Should.ThrowAsync<InvalidOperationException>(
            () => service.RecordAsync(new NewManualBalance(
                Guid.NewGuid(), new Money(10m, Currency.Euro), new DateOnly(2026, 4, 1))));
        missingAccount.Message.ShouldContain("Il conto");

        (await Context.ManualBalances.CountAsync()).ShouldBe(0);
    }

    [PostgresFact]
    public async Task Database_ShouldRejectAReadingForAMissingAccount()
    {
        var repository = new ManualBalanceRepository(Context);

        var missingAccount = await Should.ThrowAsync<DbUpdateException>(() => repository.AddAsync(
            new ManualBalance(
                Guid.NewGuid(),
                new Account(Guid.NewGuid(), "Inesistente", AccountType.Checking, Currency.Euro, AccountStatus.Active),
                new Money(10m, Currency.Euro),
                new DateOnly(2026, 4, 1),
                new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero))));
        missingAccount.ShouldNotBeNull();
    }

    [PostgresFact]
    public async Task Operations_ShouldNotCreateOrUpdateManualReadings()
    {
        var (account, category) = await AddOrdinaryContextAsync(CategoryType.Income);
        var expenseCategory = await AddCategoryAsync(CategoryType.Expense);
        var source = new Account(
            Guid.NewGuid(), "Conto due", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        await new AccountRepository(Context).AddAsync(source);
        var registration = new OperationRegistrationService(
            new AccountRepository(Context),
            new CategoryRepository(Context),
            new FinancialOperationRepository(Context));
        var manualBalances = BuildService();
        var reading = await manualBalances.RecordAsync(new NewManualBalance(
            account.Id, new Money(500m, Currency.Euro), new DateOnly(2026, 4, 1)));

        await registration.IncomeAsync(new RegisterIncomeRequest(
            category.Id,
            new PositiveMoney(new Money(100m, Currency.Euro)), new DateOnly(2026, 4, 10)));
        await registration.ExpenseAsync(new RegisterExpenseRequest(
            expenseCategory.Id,
            new PositiveMoney(new Money(30m, Currency.Euro)), new DateOnly(2026, 4, 11)));
        await registration.TransferAsync(new RegisterTransferRequest(
            source.Id, account.Id,
            new PositiveMoney(new Money(50m, Currency.Euro)),
            new PositiveMoney(new Money(50m, Currency.Euro)),
            new DateOnly(2026, 4, 12)));

        var current = (await manualBalances.GetCurrentBalanceAsync(account.Id)).ShouldNotBeNull();

        current.Amount.ShouldBe(new Money(500m, Currency.Euro));
        (await Context.ManualBalances.CountAsync(readingRecord => readingRecord.AccountId == account.Id))
            .ShouldBe(1);
    }

    [PostgresFact]
    public async Task NoReading_ShouldLeftTheBalanceExplicitlyUnavailable()
    {
        var account = await AddAccountAsync("Conto corrente");
        var service = BuildService();

        (await service.GetCurrentBalanceAsync(account.Id)).ShouldBeNull();
        (await service.GetBalanceOnOrBeforeAsync(account.Id, new DateOnly(2026, 4, 1))).ShouldBeNull();
        (await service.ListForAccountAsync(account.Id)).ShouldBeEmpty();
    }

    [PostgresFact]
    public async Task NetWorth_ShouldUseTheManualReadingsAndMarkUnreadAccountsIncomplete()
    {
        var account = await AddAccountAsync("Conto corrente");
        var unreadAccount = await AddAccountAsync("Cassa senza rilevazione");
        var service = BuildService();
        await service.RecordAsync(new NewManualBalance(
            account.Id, new Money(1234.50m, Currency.Euro), new DateOnly(2026, 4, 5)));

        var repository = new ManualBalanceRepository(Context);
        var readings = await repository.ListForAccountAsync(account.Id);

        var summary = NetWorthCalculator.Summarize(
            [account, unreadAccount], [], [], [], new DateOnly(2026, 4, 30), manualBalances: readings);

        summary.IsComplete.ShouldBeFalse();
        summary.TotalValue.ShouldBeNull();
        summary.IncompletenessReasons.ShouldContain(reason => reason.Contains(unreadAccount.Name));
        summary.Accounts.Single(entry => entry.Account.Id == account.Id)
            .Balance.ShouldBe(new Money(1234.50m, Currency.Euro));
        summary.Accounts.Single(entry => entry.Account.Id == unreadAccount.Id)
            .Balance.ShouldBeNull();
    }

    private ManualBalanceService BuildService() =>
        new(new AccountRepository(Context), new ManualBalanceRepository(Context));

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
}
