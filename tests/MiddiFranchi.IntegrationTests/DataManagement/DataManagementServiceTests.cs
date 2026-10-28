using MiddiFranchi.Application.DataManagement;
using MiddiFranchi.Application.DataManagement.Backup;
using MiddiFranchi.Application.Financial;
using MiddiFranchi.Infrastructure.DataManagement;
using MiddiFranchi.Infrastructure.Persistence.Repositories;
using MiddiFranchi.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests.DataManagement;

[Collection(PostgresCollection.Name)]
public class DataManagementServiceTests : PostgresTestBase
{
    public DataManagementServiceTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task CreateInspectDeleteRestore_ShouldRoundTripThroughService()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);
        var before = await repository.CountAsync();
        var priceStore = new FakePriceStore();
        var service = BuildService(repository, priceStore);

        await using var artifact = await service.CreateBackupAsync(
            new BackupOptions(DataSections.All, Note: "test"));

        artifact.FileName.ShouldEndWith(".mfbackup");
        artifact.Content.Length.ShouldBeGreaterThan(0);

        artifact.Content.Position = 0;
        var inspection = await service.InspectBackupAsync(artifact.Content);
        inspection.IsCompatible.ShouldBeTrue();
        inspection.IsFullRestore.ShouldBeTrue();
        inspection.Manifest.Note.ShouldBe("test");
        inspection.Sections.ShouldBe(DataSections.All);
        inspection.Manifest.Counts.FinancialOperations.ShouldBe(before.FinancialOperations);

        await repository.DeleteAsync(DataDataset.Movements, DateTimeOffset.UtcNow);
        ClearTracker();
        (await repository.CountAsync()).FinancialOperations.ShouldBe(0);

        artifact.Content.Position = 0;
        var result = await service.RestoreAsync(
            artifact.Content,
            RestoreResult.ConfirmationPhrase);
        ClearTracker();

        var after = await repository.CountAsync();
        after.Accounts.ShouldBe(before.Accounts);
        after.Categories.ShouldBe(before.Categories);
        after.Instruments.ShouldBe(before.Instruments);
        after.FinancialOperations.ShouldBe(before.FinancialOperations);
        after.AccountPostings.ShouldBe(before.AccountPostings);
        after.FeeComponents.ShouldBe(before.FeeComponents);
        after.PriceObservations.ShouldBe(before.PriceObservations);
        after.ManualBalances.ShouldBe(before.ManualBalances);
        after.Loans.ShouldBe(before.Loans);
        after.LoanReadings.ShouldBe(before.LoanReadings);
        result.Manifest.SchemaVersion.ShouldBe(inspection.Manifest.SchemaVersion);
        result.IsFullRestore.ShouldBeTrue();
        priceStore.ImportedDocument.ShouldNotBeNull();

        var metadata = await repository.ListMetadataAsync();
        metadata.ShouldContain(entry => entry.Key == AppMetadataKeys.LastRestoreAt);
    }

    [PostgresFact]
    public async Task Inspect_ShouldReportMissingReferencesForPartialMovementsBackup()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);
        var service = BuildService(repository, new FakePriceStore());

        await using var artifact = await service.CreateBackupAsync(
            new BackupOptions(DataSections.Movements));

        await repository.DeleteAsync(DataDataset.Everything, DateTimeOffset.UtcNow);
        ClearTracker();

        artifact.Content.Position = 0;
        var inspection = await service.InspectBackupAsync(artifact.Content);

        inspection.IsFullRestore.ShouldBeFalse();
        inspection.IsCompatible.ShouldBeFalse();
        inspection.Warnings.ShouldNotBeEmpty();
        inspection.Warnings.ShouldContain(warning => warning.Contains("conti"));
    }

    [PostgresFact]
    public async Task Restore_ShouldRejectWrongConfirmationPhrase()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);
        var service = BuildService(repository, new FakePriceStore());

        await using var artifact = await service.CreateBackupAsync(
            new BackupOptions(DataSections.All));
        artifact.Content.Position = 0;

        await Should.ThrowAsync<ArgumentException>(async () =>
            await service.RestoreAsync(artifact.Content, "NO"));
    }

    [PostgresFact]
    public async Task Delete_ShouldRejectWrongConfirmationPhrase()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);
        var service = BuildService(repository, new FakePriceStore());

        await Should.ThrowAsync<ArgumentException>(async () =>
            await service.DeleteAsync(new DataDeletionRequest(DataDataset.Movements, "NO")));
    }

    [PostgresFact]
    public async Task DeleteEverything_ShouldRestoreCategoriesAndSpecialAccounts()
    {
        await DataManagementScenario.SeedAsync(Context);
        var repository = new SqlDataSnapshotRepository(Context);
        var service = BuildService(repository, new FakePriceStore());

        await service.DeleteAsync(new DataDeletionRequest(
            DataDataset.Everything,
            DataDeletionRequest.ConfirmationPhrase));
        ClearTracker();

        var counts = await repository.CountAsync();
        counts.FinancialOperations.ShouldBe(0);
        counts.Instruments.ShouldBe(0);
        counts.Categories.ShouldBeGreaterThan(0);

        var categories = await new CategoryRepository(Context).ListAsync();
        categories.ShouldContain(category =>
            category.Name == "Fees" && category.Subcategory == "Conto Titoli");

        var accounts = await new AccountRepository(Context).ListAsync();
        accounts.ShouldContain(account => account.Name == "Cash");
        accounts.ShouldContain(account => account.Name == "Prestiti");
    }

    private DataManagementService BuildService(
        SqlDataSnapshotRepository repository,
        FakePriceStore priceStore)
    {
        var metadata = new AppMetadataRepository(Context);
        var accounts = new AccountRepository(Context);
        var categories = new CategoryRepository(Context);
        var manualBalances = new ManualBalanceRepository(Context);
        var loans = new LoanService(
            new LoanRepository(Context),
            new LoanReadingRepository(Context),
            accounts,
            manualBalances);

        return new DataManagementService(
            repository,
            metadata,
            priceStore,
            priceStore,
            new DataMaintenanceGate(),
            new SpecialAccountsSeeder(accounts, manualBalances, metadata),
            new DefaultCategoriesSeeder(categories, metadata),
            loans);
    }

    private sealed class FakePriceStore : IPriceUpdateStore, IPriceUpdateBackupParticipant
    {
        public string? ImportedDocument { get; private set; }

        public PriceUpdateSettings GetSettings() => PriceUpdateSettings.Default;

        public void SaveSettings(PriceUpdateSettings settings)
        {
        }

        public PriceUpdateStatus GetStatus() => PriceUpdateStatus.Empty;

        public void SaveStatus(PriceUpdateStatus status)
        {
        }

        public IReadOnlyList<PriceUpdateRun> GetHistory() => [];

        public void RecordRun(PriceUpdateRun run)
        {
        }

        public string ExportDocument() => "{\"Settings\":null,\"Status\":null}";

        public void ImportDocument(string json) => ImportedDocument = json;
    }
}
