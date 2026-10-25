using MiddiFranchi.Application.DataManagement;
using MiddiFranchi.Application.DataManagement.Backup;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class BackupSerializerTests
{
    [Fact]
    public async Task WriteAndRead_ShouldRoundTripSelectedSections()
    {
        var snapshot = SampleSnapshot();
        var sections = DataSections.Accounts | DataSections.Categories | DataSections.Instruments;
        await using var stream = new MemoryStream();

        var manifest = await BackupSerializer.WriteAsync(stream, snapshot, Manifest(sections));
        stream.Position = 0;
        var read = await BackupSerializer.ReadAsync(stream);

        manifest.Sections.ShouldBe(sections);
        manifest.Counts.ShouldBe(snapshot.Counts);
        manifest.Checksums.ShouldContainKey(BackupSerializer.AccountsEntry);
        manifest.Checksums.ShouldNotContainKey(BackupSerializer.FinancialOperationsEntry);
        read.Manifest.FormatVersion.ShouldBe(BackupManifest.CurrentFormatVersion);
        read.Manifest.Sections.ShouldBe(sections);
        read.Snapshot.Accounts.ShouldBe(snapshot.Accounts);
        read.Snapshot.Categories.ShouldBe(snapshot.Categories);
        read.Snapshot.Instruments.ShouldBe(snapshot.Instruments);
        read.Snapshot.FinancialOperations.ShouldBeEmpty();
        read.Snapshot.PriceUpdateJson.ShouldBeNull();
    }

    [Fact]
    public async Task Write_ShouldIncludeOnlySelectedSections()
    {
        var snapshot = SampleSnapshot();
        await using var stream = new MemoryStream();

        var manifest = await BackupSerializer.WriteAsync(
            stream,
            snapshot,
            Manifest(DataSections.Accounts));
        stream.Position = 0;
        var read = await BackupSerializer.ReadAsync(stream);

        manifest.Counts.Accounts.ShouldBe(1);
        manifest.Counts.Categories.ShouldBe(0);
        manifest.Checksums.Keys.ShouldBe([BackupSerializer.AccountsEntry]);
        read.Snapshot.Accounts.Count.ShouldBe(1);
        read.Snapshot.Categories.ShouldBeEmpty();
        read.Snapshot.Instruments.ShouldBeEmpty();
    }

    [Fact]
    public async Task WriteAndRead_ShouldIncludePriceSettings()
    {
        var snapshot = SampleSnapshot() with { PriceUpdateJson = "{\"Settings\":null}" };
        var sections = DataSections.PriceSettings;
        await using var stream = new MemoryStream();

        await BackupSerializer.WriteAsync(stream, snapshot, Manifest(sections));
        stream.Position = 0;
        var read = await BackupSerializer.ReadAsync(stream);

        read.Manifest.Sections.ShouldBe(DataSections.PriceSettings);
        read.Snapshot.PriceUpdateJson.ShouldBe("{\"Settings\":null}");
        read.Snapshot.Accounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Read_ShouldRejectUnsupportedFormatVersion()
    {
        await using var stream = new MemoryStream();
        var manifest = Manifest(DataSections.Accounts) with { FormatVersion = 99 };
        await BackupSerializer.WriteAsync(stream, BackupSnapshot.Empty, manifest);
        stream.Position = 0;

        await Should.ThrowAsync<BackupValidationException>(async () =>
            await BackupSerializer.ReadAsync(stream));
    }

    [Fact]
    public async Task Read_ShouldRejectReversedReversal()
    {
        var reversalId = Guid.NewGuid();
        var originalReversalId = Guid.NewGuid();
        var operations = new[]
        {
            Operation(originalReversalId, OperationKind.Reversal, Guid.NewGuid()),
            Operation(reversalId, OperationKind.Reversal, originalReversalId),
        };

        var snapshot = BackupSnapshot.Empty with
        {
            FinancialOperations = operations,
        };
        await using var stream = new MemoryStream();
        await BackupSerializer.WriteAsync(stream, snapshot, Manifest(DataSections.Movements));
        stream.Position = 0;

        await Should.ThrowAsync<BackupValidationException>(async () =>
            await BackupSerializer.ReadAsync(stream));
    }

    private static BackupManifest Manifest(DataSections sections) => new(
        BackupManifest.CurrentFormatVersion,
        "1.0.0",
        "20260101000000_Test",
        DateTimeOffset.UtcNow,
        "PostgreSQL",
        "test",
        sections,
        DataCounts.Empty,
        new Dictionary<string, string>(StringComparer.Ordinal));

    private static BackupSnapshot SampleSnapshot() => new(
        Accounts:
        [
            new AccountBackup(
                Guid.NewGuid(),
                "Conto",
                AccountType.Checking,
                "EUR",
                AccountStatus.Active,
                "#112233",
                null),
        ],
        Categories:
        [
            new CategoryBackup(
                Guid.NewGuid(),
                "Spesa",
                CategoryType.Expense,
                null,
                "Alimentazione",
                "utensils",
                "#22c55e"),
        ],
        Instruments:
        [
            new InstrumentBackup(
                Guid.NewGuid(),
                "ETF",
                "VWCE",
                "IE00BK5BQT80",
                AssetClass.Equity,
                InstrumentStatus.Active,
                "EUR",
                PriceUnit.PerUnit,
                null,
                null,
                null,
                null,
                null,
                null),
        ],
        FinancialOperations: [],
        AccountPostings: [],
        FeeComponents: [],
        PriceObservations: [],
        ManualBalances: [],
        Loans: [],
        LoanReadings: []);

    private static FinancialOperationBackup Operation(Guid id, OperationKind kind, Guid? originalId) =>
        new(
            id,
            kind,
            new DateOnly(2026, 1, 1),
            DateTimeOffset.UtcNow,
            null,
            originalId,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
}
