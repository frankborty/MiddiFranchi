using MiddiFranchi.Application.DataManagement.Backup;
using MiddiFranchi.Application.Persistence;

namespace MiddiFranchi.Application.DataManagement;

public sealed record SchemaVersionInfo(string AppliedVersion, string ExpectedVersion)
{
    public bool IsUpToDate =>
        string.Equals(AppliedVersion, ExpectedVersion, StringComparison.Ordinal);
}

public sealed record RestoreReferenceCheck(bool IsValid, IReadOnlyList<string> Issues)
{
    public static RestoreReferenceCheck Valid { get; } = new(true, []);
}

public interface IDataSnapshotRepository
{
    Task<DataCounts> CountAsync(CancellationToken cancellationToken = default);

    Task<SchemaVersionInfo> GetSchemaVersionAsync(CancellationToken cancellationToken = default);

    Task<BackupSnapshot> ReadAsync(
        DataSections sections,
        CancellationToken cancellationToken = default);

    Task<RestoreReferenceCheck> CheckReferencesAsync(
        BackupSnapshot snapshot,
        DataSections sections,
        CancellationToken cancellationToken = default);

    Task RestoreAsync(
        BackupSnapshot snapshot,
        DataSections sections,
        DateTimeOffset restoredAt,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        DataDataset datasets,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppMetadataEntry>> ListMetadataAsync(
        CancellationToken cancellationToken = default);
}
