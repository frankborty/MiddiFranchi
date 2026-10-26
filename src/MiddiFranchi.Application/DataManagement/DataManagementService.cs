using MiddiFranchi.Application.DataManagement.Backup;
using MiddiFranchi.Application.Financial;
using MiddiFranchi.Application.Persistence;

namespace MiddiFranchi.Application.DataManagement;

public sealed class DataManagementService
{
    private const string DatabaseProvider = "PostgreSQL";

    private readonly IDataSnapshotRepository _snapshots;
    private readonly IAppMetadataRepository _metadata;
    private readonly IPriceUpdateStore _priceStore;
    private readonly IPriceUpdateBackupParticipant _priceBackup;
    private readonly DataMaintenanceGate _gate;
    private readonly SpecialAccountsSeeder _specialAccounts;
    private readonly DefaultCategoriesSeeder _defaultCategories;
    private readonly LoanService _loans;

    public DataManagementService(
        IDataSnapshotRepository snapshots,
        IAppMetadataRepository metadata,
        IPriceUpdateStore priceStore,
        IPriceUpdateBackupParticipant priceBackup,
        DataMaintenanceGate gate,
        SpecialAccountsSeeder specialAccounts,
        DefaultCategoriesSeeder defaultCategories,
        LoanService loans)
    {
        _snapshots = snapshots;
        _metadata = metadata;
        _priceStore = priceStore;
        _priceBackup = priceBackup;
        _gate = gate;
        _specialAccounts = specialAccounts;
        _defaultCategories = defaultCategories;
        _loans = loans;
    }

    public async Task<DataOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var counts = await _snapshots.CountAsync(cancellationToken).ConfigureAwait(false);
        var schema = await _snapshots.GetSchemaVersionAsync(cancellationToken).ConfigureAwait(false);

        return new DataOverview(counts, schema);
    }

    public async Task<DataDeletionPlan> GetDeletionPlanAsync(
        DataDataset selection,
        CancellationToken cancellationToken = default)
    {
        var counts = await _snapshots.CountAsync(cancellationToken).ConfigureAwait(false);
        return DataDeletionPlanner.Build(selection, counts);
    }

    public async Task<BackupArtifact> CreateBackupAsync(
        BackupOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var sections = options.Sections.Normalize();
        if (sections == DataSections.None)
        {
            throw new BackupValidationException("Seleziona almeno una sezione da includere nel backup.");
        }

        using var handle = await _gate.EnterAsync(cancellationToken).ConfigureAwait(false);

        var schema = await _snapshots.GetSchemaVersionAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await _snapshots.ReadAsync(sections, cancellationToken).ConfigureAwait(false);

        if (sections.Includes(DataSections.PriceSettings))
        {
            snapshot = snapshot with { PriceUpdateJson = _priceBackup.ExportDocument() };
        }

        var manifest = new BackupManifest(
            FormatVersion: BackupManifest.CurrentFormatVersion,
            AppVersion: typeof(DataManagementService).Assembly.GetName().Version?.ToString() ?? "unknown",
            SchemaVersion: schema.AppliedVersion,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            DatabaseProvider: DatabaseProvider,
            Note: string.IsNullOrWhiteSpace(options.Note) ? null : options.Note.Trim(),
            Sections: sections,
            Counts: snapshot.Counts,
            Checksums: new Dictionary<string, string>(StringComparer.Ordinal));

        var directory = Path.Combine(Path.GetTempPath(), "MiddiFranchi", "backups");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".mfbackup");
        var stream = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read,
            81920,
            useAsync: true);

        try
        {
            await BackupSerializer.WriteAsync(stream, snapshot, manifest, cancellationToken)
                .ConfigureAwait(false);
            stream.Position = 0;

            var fileName = $"MiddiFranchi-backup-{DateTime.Now:yyyyMMdd-HHmmss}.mfbackup";
            return new BackupArtifact(
                fileName,
                "application/octet-stream",
                stream,
                temporaryPath);
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            TryDelete(temporaryPath);
            throw;
        }
    }

    public async Task<BackupInspection> InspectBackupAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var read = await BackupSerializer.ReadAsync(source, cancellationToken)
            .ConfigureAwait(false);
        var sections = read.Manifest.Sections.Normalize();
        var schema = await _snapshots.GetSchemaVersionAsync(cancellationToken).ConfigureAwait(false);

        var warnings = new List<string>();
        var compatible = schema.IsUpToDate && string.Equals(
            read.Manifest.SchemaVersion,
            schema.AppliedVersion,
            StringComparison.Ordinal);

        if (!compatible)
        {
            warnings.Add(
                $"Schema incompatibile: il backup è stato creato con «{read.Manifest.SchemaVersion}», " +
                $"mentre il database corrente applica «{schema.AppliedVersion}». " +
                "Aggiorna il nodo o usa un backup della stessa versione.");
        }

        if (!schema.IsUpToDate)
        {
            warnings.Add(
                $"Il database non è allineato alle migration dell'applicazione: " +
                $"applicata «{schema.AppliedVersion}», attesa «{schema.ExpectedVersion}». " +
                "Esegui `dotnet ef database update` prima di procedere.");
        }

        var references = await _snapshots
            .CheckReferencesAsync(read.Snapshot, sections, cancellationToken)
            .ConfigureAwait(false);
        if (!references.IsValid)
        {
            compatible = false;
            warnings.AddRange(references.Issues);
        }

        return new BackupInspection(
            read.Manifest,
            sections,
            sections.IsFullDataRestore(),
            compatible,
            warnings);
    }

    public async Task<RestoreResult> RestoreAsync(
        Stream source,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!string.Equals(confirmation, RestoreResult.ConfirmationPhrase, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Per confermare il ripristino digita «{RestoreResult.ConfirmationPhrase}».",
                nameof(confirmation));
        }

        var read = await BackupSerializer.ReadAsync(source, cancellationToken)
            .ConfigureAwait(false);
        var sections = read.Manifest.Sections.Normalize();
        if (sections == DataSections.None)
        {
            throw new BackupValidationException("Il backup non contiene sezioni dati.");
        }

        var schema = await _snapshots.GetSchemaVersionAsync(cancellationToken).ConfigureAwait(false);

        if (!schema.IsUpToDate)
        {
            throw new BackupValidationException(
                $"Il database non è allineato alle migration dell'applicazione: " +
                $"applicata «{schema.AppliedVersion}», attesa «{schema.ExpectedVersion}». " +
                "Esegui `dotnet ef database update` prima di ripristinare.");
        }

        if (!string.Equals(
                read.Manifest.SchemaVersion,
                schema.AppliedVersion,
                StringComparison.Ordinal))
        {
            throw new BackupValidationException(
                $"Schema incompatibile: il backup usa «{read.Manifest.SchemaVersion}», " +
                $"il database corrente «{schema.AppliedVersion}».");
        }

        var references = await _snapshots
            .CheckReferencesAsync(read.Snapshot, sections, cancellationToken)
            .ConfigureAwait(false);
        if (!references.IsValid)
        {
            throw new BackupValidationException(string.Join(" ", references.Issues));
        }

        using var handle = await _gate.EnterAsync(cancellationToken).ConfigureAwait(false);

        var restoredAt = DateTimeOffset.UtcNow;
        await _snapshots.RestoreAsync(read.Snapshot, sections, restoredAt, cancellationToken)
            .ConfigureAwait(false);

        var warnings = new List<string>();
        if (sections.Includes(DataSections.Instruments))
        {
            _priceStore.SaveStatus(PriceUpdateStatus.Empty);
        }

        if (sections.Includes(DataSections.PriceSettings) &&
            read.Snapshot.PriceUpdateJson is not null)
        {
            try
            {
                _priceBackup.ImportDocument(read.Snapshot.PriceUpdateJson);
            }
            catch (Exception exception)
            {
                warnings.Add(
                    "I dati sono stati ripristinati, ma non è stato possibile importare le impostazioni " +
                    $"del servizio prezzi: {exception.Message}");
            }
        }

        return new RestoreResult(
            read.Manifest,
            sections,
            sections.IsFullDataRestore(),
            restoredAt,
            warnings);
    }

    public async Task<DataDeletionResult> DeleteAsync(
        DataDeletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(
                request.Confirmation,
                DataDeletionRequest.ConfirmationPhrase,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Per confermare l'eliminazione digita «{DataDeletionRequest.ConfirmationPhrase}».",
                nameof(request));
        }

        var counts = await _snapshots.CountAsync(cancellationToken).ConfigureAwait(false);
        var plan = DataDeletionPlanner.Build(request.Datasets, counts);
        if (plan.IsEmpty)
        {
            throw new ArgumentException("Seleziona almeno un insieme di dati da eliminare.", nameof(request));
        }

        using var handle = await _gate.EnterAsync(cancellationToken).ConfigureAwait(false);

        var deletedAt = DateTimeOffset.UtcNow;
        await _snapshots.DeleteAsync(plan.Effective, deletedAt, cancellationToken)
            .ConfigureAwait(false);

        // Le categorie e i conti speciali sono dati di configurazione necessari all'app:
        // quando vengono eliminati vanno ricreati subito, altrimenti i marker di seed
        // (impostati a "done" dalla cancellazione) impedirebbero il ripristino.
        if (plan.Effective.Includes(DataDataset.Accounts))
        {
            await _metadata.RemoveAsync(AppMetadataKeys.SeedSpecialAccounts, cancellationToken)
                .ConfigureAwait(false);
            await _specialAccounts.EnsureAsync(cancellationToken).ConfigureAwait(false);
            await _loans.RebuildAllAsync(cancellationToken).ConfigureAwait(false);
        }

        if (plan.Effective.Includes(DataDataset.Categories))
        {
            await _metadata.RemoveAsync(AppMetadataKeys.SeedCategories, cancellationToken)
                .ConfigureAwait(false);
            await _defaultCategories.EnsureAsync(cancellationToken).ConfigureAwait(false);
        }

        var priceStatusReset = false;
        if (plan.Effective.Includes(DataDataset.Instruments))
        {
            _priceStore.SaveStatus(PriceUpdateStatus.Empty);
            priceStatusReset = true;
        }

        return new DataDeletionResult(plan, deletedAt, priceStatusReset);
    }

    public async Task ResetDefaultsAsync(CancellationToken cancellationToken = default)
    {
        using var handle = await _gate.EnterAsync(cancellationToken).ConfigureAwait(false);

        await _metadata.RemoveAsync(AppMetadataKeys.SeedSpecialAccounts, cancellationToken)
            .ConfigureAwait(false);
        await _metadata.RemoveAsync(AppMetadataKeys.SeedCategories, cancellationToken)
            .ConfigureAwait(false);

        await _specialAccounts.EnsureAsync(cancellationToken).ConfigureAwait(false);
        await _defaultCategories.EnsureAsync(cancellationToken).ConfigureAwait(false);
        await _loans.RebuildAllAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
