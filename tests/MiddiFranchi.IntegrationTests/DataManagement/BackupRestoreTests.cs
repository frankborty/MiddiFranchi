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
public class BackupRestoreTests : PostgresTestBase
{
    public BackupRestoreTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task ReadRestoreRead_ShouldPreserveAllSectionsAndIdentifiers()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);

        var before = await repository.ReadAsync(DataSections.AllData);
        before.Accounts.Count.ShouldBeGreaterThanOrEqualTo(3);
        before.Categories.Count.ShouldBeGreaterThanOrEqualTo(2);
        before.Instruments.Count.ShouldBeGreaterThanOrEqualTo(1);
        before.FinancialOperations.Count.ShouldBeGreaterThanOrEqualTo(4);
        before.AccountPostings.Count.ShouldBeGreaterThanOrEqualTo(1);
        before.FeeComponents.Count.ShouldBeGreaterThanOrEqualTo(1);
        before.PriceObservations.Count.ShouldBeGreaterThanOrEqualTo(1);
        before.ManualBalances.Count.ShouldBeGreaterThanOrEqualTo(1);
        before.Loans.Count.ShouldBeGreaterThanOrEqualTo(1);
        before.LoanReadings.Count.ShouldBeGreaterThanOrEqualTo(1);

        await repository.RestoreAsync(before, DataSections.AllData, DateTimeOffset.UtcNow);
        ClearTracker();

        var after = await repository.ReadAsync(DataSections.AllData);

        after.Accounts.ShouldBe(before.Accounts, ignoreOrder: true);
        after.Categories.ShouldBe(before.Categories, ignoreOrder: true);
        after.Instruments.ShouldBe(before.Instruments, ignoreOrder: true);
        after.FinancialOperations.ShouldBe(before.FinancialOperations, ignoreOrder: true);
        after.AccountPostings.ShouldBe(before.AccountPostings, ignoreOrder: true);
        after.FeeComponents.ShouldBe(before.FeeComponents, ignoreOrder: true);
        after.PriceObservations.ShouldBe(before.PriceObservations, ignoreOrder: true);
        after.ManualBalances.ShouldBe(before.ManualBalances, ignoreOrder: true);
        after.Loans.ShouldBe(before.Loans, ignoreOrder: true);
        after.LoanReadings.ShouldBe(before.LoanReadings, ignoreOrder: true);

        var metadata = await repository.ListMetadataAsync();
        metadata.ShouldContain(
            entry => entry.Key == AppMetadataKeys.SeedSpecialAccounts && entry.Value == "done");
        metadata.ShouldContain(
            entry => entry.Key == AppMetadataKeys.SeedCategories && entry.Value == "done");
        metadata.ShouldContain(entry => entry.Key == AppMetadataKeys.LastRestoreAt);
        metadata.ShouldContain(entry => entry.Key == "test.key" && entry.Value == "test-value");
    }

    [PostgresFact]
    public async Task FullRestore_ShouldReplaceExistingDataNotPresentInBackup()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);
        var snapshot = await repository.ReadAsync(DataSections.AllData);

        var accounts = new AccountRepository(Context);
        await accounts.AddAsync(new Account(
            Guid.NewGuid(),
            "Conto aggiunto dopo il backup",
            AccountType.Cash,
            Currency.Euro,
            AccountStatus.Active));
        ClearTracker();
        (await repository.CountAsync()).Accounts.ShouldBe(snapshot.Counts.Accounts + 1);

        await repository.RestoreAsync(snapshot, DataSections.AllData, DateTimeOffset.UtcNow);
        ClearTracker();

        var after = await repository.ReadAsync(DataSections.AllData);
        after.Accounts.Count.ShouldBe(snapshot.Counts.Accounts);
        after.Accounts.ShouldNotContain(account => account.Name == "Conto aggiunto dopo il backup");
    }

    [PostgresFact]
    public async Task RestoreWithEmptySnapshot_ShouldClearAllDataTables()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);

        await repository.RestoreAsync(
            BackupSnapshot.Empty,
            DataSections.AllData,
            DateTimeOffset.UtcNow);
        ClearTracker();

        var counts = await repository.CountAsync();

        counts.Accounts.ShouldBe(0);
        counts.Categories.ShouldBe(0);
        counts.Instruments.ShouldBe(0);
        counts.FinancialOperations.ShouldBe(0);
        counts.AccountPostings.ShouldBe(0);
        counts.FeeComponents.ShouldBe(0);
        counts.PriceObservations.ShouldBe(0);
        counts.ManualBalances.ShouldBe(0);
        counts.Loans.ShouldBe(0);
        counts.LoanReadings.ShouldBe(0);

        var metadata = await repository.ListMetadataAsync();
        metadata.ShouldContain(
            entry => entry.Key == AppMetadataKeys.SeedSpecialAccounts && entry.Value == "done");
        metadata.ShouldContain(entry => entry.Key == AppMetadataKeys.LastRestoreAt);
    }
}
