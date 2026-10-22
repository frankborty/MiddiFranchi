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
public class OperationRegistrationFlowTests : PostgresTestBase
{
    public OperationRegistrationFlowTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task Registrations_ShouldOnlyCreatePostingsForTransfers_AndLeaveManualReadingsUntouched()
    {
        var accountRepository = new AccountRepository(Context);
        var source = new Account(
            Guid.NewGuid(), "Conto corrente", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        var destination = new Account(
            Guid.NewGuid(), "Cassa", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        await accountRepository.AddAsync(source);
        await accountRepository.AddAsync(destination);
        var incomeCategory = await AddCategoryAsync(CategoryType.Income);
        var expenseCategory = await AddCategoryAsync(CategoryType.Expense);

        var manualBalances = new ManualBalanceRepository(Context);
        var reading = new ManualBalance(
            Guid.NewGuid(), source,
            new Money(500m, Currency.Euro),
            new DateOnly(2026, 4, 1),
            new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        await manualBalances.AddAsync(reading);
        ClearTracker();

        var registrations = new OperationRegistrationService(
            new AccountRepository(Context),
            new CategoryRepository(Context),
            new FinancialOperationRepository(Context));
        var date = new DateOnly(2026, 4, 10);

        await registrations.IncomeAsync(new RegisterIncomeRequest(
            incomeCategory.Id,
            new PositiveMoney(new Money(120m, Currency.Euro)), date));
        await registrations.ExpenseAsync(new RegisterExpenseRequest(
            expenseCategory.Id,
            new PositiveMoney(new Money(30m, Currency.Euro)), date.AddDays(1)));
        await registrations.TransferAsync(new RegisterTransferRequest(
            source.Id, destination.Id,
            new PositiveMoney(new Money(40m, Currency.Euro)),
            new PositiveMoney(new Money(40m, Currency.Euro)),
            date.AddDays(2)));
        ClearTracker();

        var movements = await new AccountPostingRepository(Context).ListAsync(source.Id, null, null);

        // Entrate e uscite non hanno conto: l'unico posting sul conto è la gamba del trasferimento.
        movements.Count.ShouldBe(1);
        movements.ShouldHaveSingleItem().SignedAmount.ShouldBe(-40m);

        var destinationMovements = await new AccountPostingRepository(Context).ListAsync(destination.Id, null, null);
        destinationMovements.ShouldHaveSingleItem().SignedAmount.ShouldBe(40m);

        // La registrazione delle operazioni non deve toccare le rilevazioni manuali.
        (await Context.ManualBalances.CountAsync(readingRecord => readingRecord.AccountId == source.Id))
            .ShouldBe(1);
        (await manualBalances.GetLatestAsync(source.Id)).ShouldNotBeNull().Id.ShouldBe(reading.Id);
    }

    [PostgresFact]
    public async Task UnbalancedTransfers_ShouldBeRejected()
    {
        var source = new Account(
            Guid.NewGuid(), "Conto corrente", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        var destination = new Account(
            Guid.NewGuid(), "Cassa", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        await new AccountRepository(Context).AddAsync(source);
        await new AccountRepository(Context).AddAsync(destination);
        var registrations = new OperationRegistrationService(
            new AccountRepository(Context),
            new CategoryRepository(Context),
            new FinancialOperationRepository(Context));

        _ = await Should.ThrowAsync<ArgumentException>(() => registrations.TransferAsync(new RegisterTransferRequest(
            source.Id, destination.Id,
            new PositiveMoney(new Money(100m, Currency.Euro)),
            new PositiveMoney(new Money(80m, Currency.Euro)),
            new DateOnly(2026, 4, 10))));

        (await Context.FinancialOperations.CountAsync()).ShouldBe(0);
    }

    private async Task<Category> AddCategoryAsync(CategoryType type)
    {
        var category = new Category(Guid.NewGuid(), "Categoria", type);
        await new CategoryRepository(Context).AddAsync(category);
        return category;
    }
}
