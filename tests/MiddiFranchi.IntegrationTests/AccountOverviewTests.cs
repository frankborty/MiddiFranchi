using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.Persistence.Repositories;
using MiddiFranchi.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class AccountOverviewTests : PostgresTestBase
{
    public AccountOverviewTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task Overview_ShouldShowTheUpdatedReadingRightAfterRegistration()
    {
        var account = new Account(
            Guid.NewGuid(), "Conto corrente", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        await new AccountRepository(Context).AddAsync(account);
        var overview = BuildOverviewService();
        var manualBalances = BuildManualBalanceService();

        var before = (await overview.GetAsync(account.Id)).ShouldNotBeNull();
        before.ManualBalance.ShouldBeNull();
        before.CalculatedBalance.ShouldBe(new Money(0m, Currency.Euro));

        var reading = await manualBalances.RecordAsync(new NewManualBalance(
            account.Id,
            new Money(1234.5678901234567890m, Currency.Euro),
            new DateOnly(2026, 4, 1)));
        ClearTracker();

        var after = (await overview.GetAsync(account.Id)).ShouldNotBeNull();

        after.ManualReading.ShouldNotBeNull().Id.ShouldBe(reading.Id);
        after.ManualBalance.ShouldBe(reading.Amount);
        after.ManualBalance!.Value.Amount.ShouldBe(1234.5678901234567890m);
        // L'entrata non cambia il saldo manuale; il saldo ricostruito resta la metrica secondaria.
        after.CalculatedBalance.ShouldBe(new Money(0m, Currency.Euro));
    }

    [PostgresFact]
    public async Task Overview_List_ShouldReturnAllAccountsWithOrderOffTheRepository()
    {
        var repository = new AccountRepository(Context);
        var first = new Account(
            Guid.NewGuid(), "Conto corrente", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        var second = new Account(
            Guid.NewGuid(), "Cassa", AccountType.Cash, Currency.Euro, AccountStatus.Active);
        await repository.AddAsync(first);
        await repository.AddAsync(second);
        ClearTracker();

        var rows = await BuildOverviewService().ListAsync();

        rows.Select(row => row.Name).ShouldBe(["Cassa", "Conto corrente"]);
        rows.ShouldAllBe(row => row.ManualBalance.HasValue == false);
        rows.ShouldAllBe(row => row.CalculatedBalance!.Value.Amount == 0m);
    }

    private AccountOverviewService BuildOverviewService() =>
        new(
            new AccountRepository(Context),
            new ManualBalanceRepository(Context),
            new AccountPostingRepository(Context));

    private ManualBalanceService BuildManualBalanceService() =>
        new(new AccountRepository(Context), new ManualBalanceRepository(Context));
}
