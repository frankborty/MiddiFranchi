using MiddiFranchi.Application.DataManagement;
using MiddiFranchi.Infrastructure.DataManagement;
using MiddiFranchi.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests.DataManagement;

[Collection(PostgresCollection.Name)]
public class SelectiveDeletionTests : PostgresTestBase
{
    public SelectiveDeletionTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task DeleteMovements_ShouldKeepAccountsInstrumentsAndCategories()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);
        var before = await repository.CountAsync();

        await repository.DeleteAsync(DataDataset.Movements, DateTimeOffset.UtcNow);
        ClearTracker();

        var counts = await repository.CountAsync();

        counts.FinancialOperations.ShouldBe(0);
        counts.AccountPostings.ShouldBe(0);
        counts.FeeComponents.ShouldBe(0);
        counts.Accounts.ShouldBe(before.Accounts);
        counts.Categories.ShouldBe(before.Categories);
        counts.Instruments.ShouldBe(before.Instruments);
        counts.PriceObservations.ShouldBe(before.PriceObservations);
        counts.ManualBalances.ShouldBe(before.ManualBalances);
        counts.Loans.ShouldBe(before.Loans);
        counts.LoanReadings.ShouldBe(before.LoanReadings);

        var metadata = await repository.ListMetadataAsync();
        metadata.ShouldContain(entry => entry.Key == AppMetadataKeys.LastDeletionAt);
        metadata.ShouldContain(
            entry => entry.Key == AppMetadataKeys.SeedSpecialAccounts && entry.Value == "done");
    }

    [PostgresFact]
    public async Task DeleteInstrumentsPlan_ShouldAlsoClearMovementsAndObservations()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);
        var counts = await repository.CountAsync();
        var plan = DataDeletionPlanner.Build(DataDataset.Instruments, counts);

        await repository.DeleteAsync(plan.Effective, DateTimeOffset.UtcNow);
        ClearTracker();

        var after = await repository.CountAsync();

        after.Instruments.ShouldBe(0);
        after.PriceObservations.ShouldBe(0);
        after.FinancialOperations.ShouldBe(0);
        after.AccountPostings.ShouldBe(0);
        after.FeeComponents.ShouldBe(0);
        after.Accounts.ShouldBe(counts.Accounts);
        after.Categories.ShouldBe(counts.Categories);
        after.ManualBalances.ShouldBe(counts.ManualBalances);
        after.Loans.ShouldBe(counts.Loans);
        after.LoanReadings.ShouldBe(counts.LoanReadings);
    }

    [PostgresFact]
    public async Task DeleteEverything_ShouldClearDataAndLeaveOnlySeedMetadata()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);

        await repository.DeleteAsync(DataDataset.Everything, DateTimeOffset.UtcNow);
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
        counts.AppMetadata.ShouldBe(3);

        var metadata = await repository.ListMetadataAsync();
        metadata.ShouldContain(
            entry => entry.Key == AppMetadataKeys.SeedSpecialAccounts && entry.Value == "done");
        metadata.ShouldContain(
            entry => entry.Key == AppMetadataKeys.SeedCategories && entry.Value == "done");
        metadata.ShouldContain(entry => entry.Key == AppMetadataKeys.LastDeletionAt);
    }
}
