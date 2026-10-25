using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Application.DataManagement;
using MiddiFranchi.Application.DataManagement.Backup;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.DataManagement;
using MiddiFranchi.Infrastructure.Persistence.Repositories;
using MiddiFranchi.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests.DataManagement;

[Collection(PostgresCollection.Name)]
public class PartialRestoreTests : PostgresTestBase
{
    public PartialRestoreTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task MovementsRestore_ShouldReplaceMovementsOnly()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);
        var movements = await repository.ReadAsync(DataSections.Movements);
        var before = await repository.CountAsync();

        await repository.DeleteAsync(DataDataset.Movements, DateTimeOffset.UtcNow);
        ClearTracker();
        (await repository.CountAsync()).FinancialOperations.ShouldBe(0);

        var check = await repository.CheckReferencesAsync(movements, DataSections.Movements);
        check.IsValid.ShouldBeTrue();

        await repository.RestoreAsync(movements, DataSections.Movements, DateTimeOffset.UtcNow);
        ClearTracker();

        var after = await repository.CountAsync();
        after.FinancialOperations.ShouldBe(movements.Counts.FinancialOperations);
        after.AccountPostings.ShouldBe(movements.Counts.AccountPostings);
        after.FeeComponents.ShouldBe(movements.Counts.FeeComponents);
        after.Accounts.ShouldBe(before.Accounts);
        after.Categories.ShouldBe(before.Categories);
        after.Instruments.ShouldBe(before.Instruments);
        after.PriceObservations.ShouldBe(before.PriceObservations);
        after.ManualBalances.ShouldBe(before.ManualBalances);
        after.Loans.ShouldBe(before.Loans);
        after.LoanReadings.ShouldBe(before.LoanReadings);
    }

    [PostgresFact]
    public async Task AccountsRestore_ShouldUpsertAndKeepExistingExtras()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);
        var accounts = await repository.ReadAsync(DataSections.Accounts);
        var original = accounts.Accounts[0];

        var record = await Context.Accounts.SingleAsync(account => account.Id == original.Id);
        record.Name = "Nome modificato";
        await Context.SaveChangesAsync();
        ClearTracker();

        var accountsRepository = new AccountRepository(Context);
        await accountsRepository.AddAsync(new Account(
            Guid.NewGuid(),
            "Conto extra",
            AccountType.Cash,
            Currency.Euro,
            AccountStatus.Active));
        ClearTracker();

        await repository.RestoreAsync(accounts, DataSections.Accounts, DateTimeOffset.UtcNow);
        ClearTracker();

        var after = await repository.ReadAsync(DataSections.Accounts);
        after.Accounts.Count.ShouldBe(accounts.Accounts.Count + 1);
        after.Accounts.Single(account => account.Id == original.Id).Name.ShouldBe(original.Name);
        after.Accounts.ShouldContain(account => account.Name == "Conto extra");
    }

    [PostgresFact]
    public async Task PriceHistoryRestore_ShouldReplaceObservationsOnly()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);
        var prices = await repository.ReadAsync(DataSections.PriceHistory);
        var before = await repository.CountAsync();

        await repository.RestoreAsync(
            BackupSnapshot.Empty,
            DataSections.PriceHistory,
            DateTimeOffset.UtcNow);
        ClearTracker();
        (await repository.CountAsync()).PriceObservations.ShouldBe(0);

        await repository.RestoreAsync(prices, DataSections.PriceHistory, DateTimeOffset.UtcNow);
        ClearTracker();

        var after = await repository.CountAsync();
        after.PriceObservations.ShouldBe(prices.Counts.PriceObservations);
        after.Instruments.ShouldBe(before.Instruments);
        after.Accounts.ShouldBe(before.Accounts);
        after.FinancialOperations.ShouldBe(before.FinancialOperations);
    }

    [PostgresFact]
    public async Task AccountHistoryRestore_ShouldReplaceHistoryOnly()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);
        var history = await repository.ReadAsync(DataSections.AccountHistory);
        var before = await repository.CountAsync();

        await repository.RestoreAsync(
            BackupSnapshot.Empty,
            DataSections.AccountHistory,
            DateTimeOffset.UtcNow);
        ClearTracker();
        var cleared = await repository.CountAsync();
        cleared.ManualBalances.ShouldBe(0);
        cleared.Loans.ShouldBe(0);
        cleared.LoanReadings.ShouldBe(0);

        await repository.RestoreAsync(history, DataSections.AccountHistory, DateTimeOffset.UtcNow);
        ClearTracker();

        var after = await repository.CountAsync();
        after.ManualBalances.ShouldBe(history.Counts.ManualBalances);
        after.Loans.ShouldBe(history.Counts.Loans);
        after.LoanReadings.ShouldBe(history.Counts.LoanReadings);
        after.Accounts.ShouldBe(before.Accounts);
        after.FinancialOperations.ShouldBe(before.FinancialOperations);
    }

    [PostgresFact]
    public async Task CheckReferences_ShouldReportMissingAccounts()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);
        var movements = await repository.ReadAsync(DataSections.Movements);

        await repository.DeleteAsync(DataDataset.Everything, DateTimeOffset.UtcNow);
        ClearTracker();

        var check = await repository.CheckReferencesAsync(movements, DataSections.Movements);

        check.IsValid.ShouldBeFalse();
        check.Issues.ShouldContain(issue => issue.Contains("conti"));
    }
}
