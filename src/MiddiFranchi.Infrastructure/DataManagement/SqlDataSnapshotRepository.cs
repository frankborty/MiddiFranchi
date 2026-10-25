using System.Data;
using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Application.DataManagement;
using MiddiFranchi.Application.DataManagement.Backup;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Infrastructure.Persistence;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.DataManagement;

public sealed class SqlDataSnapshotRepository : IDataSnapshotRepository
{
    private const string TruncateAllDataSql =
        "TRUNCATE TABLE \"Accounts\", \"Categories\", \"Instruments\", \"FinancialOperations\", " +
        "\"AccountPostings\", \"InvestmentOperationFees\", \"PriceObservations\", " +
        "\"ManualBalances\", \"Loans\", \"LoanReadings\" RESTART IDENTITY;";

    private const string TruncateMovementsSql =
        "TRUNCATE TABLE \"FinancialOperations\", \"AccountPostings\", " +
        "\"InvestmentOperationFees\" RESTART IDENTITY;";

    private const string TruncateAccountHistorySql =
        "TRUNCATE TABLE \"LoanReadings\", \"Loans\", \"ManualBalances\" RESTART IDENTITY;";

    private const string TruncatePriceHistorySql =
        "TRUNCATE TABLE \"PriceObservations\" RESTART IDENTITY;";

    private readonly MiddiFranchiDbContext _context;

    public SqlDataSnapshotRepository(MiddiFranchiDbContext context)
    {
        _context = context;
    }

    public async Task<DataCounts> CountAsync(CancellationToken cancellationToken = default) => new(
        Accounts: await _context.Accounts.CountAsync(cancellationToken).ConfigureAwait(false),
        Categories: await _context.Categories.CountAsync(cancellationToken).ConfigureAwait(false),
        Instruments: await _context.Instruments.CountAsync(cancellationToken).ConfigureAwait(false),
        FinancialOperations: await _context.FinancialOperations.CountAsync(cancellationToken)
            .ConfigureAwait(false),
        AccountPostings: await _context.AccountPostings.CountAsync(cancellationToken)
            .ConfigureAwait(false),
        FeeComponents: await _context.Set<FeeComponentRecord>().CountAsync(cancellationToken)
            .ConfigureAwait(false),
        PriceObservations: await _context.PriceObservations.CountAsync(cancellationToken)
            .ConfigureAwait(false),
        ManualBalances: await _context.ManualBalances.CountAsync(cancellationToken)
            .ConfigureAwait(false),
        Loans: await _context.Loans.CountAsync(cancellationToken).ConfigureAwait(false),
        LoanReadings: await _context.LoanReadings.CountAsync(cancellationToken).ConfigureAwait(false),
        AppMetadata: await _context.AppMetadata.CountAsync(cancellationToken).ConfigureAwait(false));

    public async Task<SchemaVersionInfo> GetSchemaVersionAsync(
        CancellationToken cancellationToken = default)
    {
        var expected = _context.Database.GetMigrations()
            .OrderBy(migration => migration, StringComparer.Ordinal)
            .LastOrDefault() ?? "";

        var applied = (await _context.Database.GetAppliedMigrationsAsync(cancellationToken)
                .ConfigureAwait(false))
            .OrderBy(migration => migration, StringComparer.Ordinal)
            .LastOrDefault() ?? "";

        return new SchemaVersionInfo(applied, expected);
    }

    public async Task<BackupSnapshot> ReadAsync(
        DataSections sections,
        CancellationToken cancellationToken = default)
    {
        var normalized = sections.Normalize();
        var ownsTransaction = _context.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await _context.Database
                .BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
                .ConfigureAwait(false)
            : null;

        try
        {
            var accounts = normalized.Includes(DataSections.Accounts)
                ? await _context.Accounts.AsNoTracking().ToListAsync(cancellationToken)
                    .ConfigureAwait(false)
                : [];
            var categories = normalized.Includes(DataSections.Categories)
                ? await _context.Categories.AsNoTracking().ToListAsync(cancellationToken)
                    .ConfigureAwait(false)
                : [];
            var instruments = normalized.Includes(DataSections.Instruments)
                ? await _context.Instruments.AsNoTracking().ToListAsync(cancellationToken)
                    .ConfigureAwait(false)
                : [];
            var operations = normalized.Includes(DataSections.Movements)
                ? await _context.FinancialOperations.AsNoTracking()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false)
                : [];
            var postings = normalized.Includes(DataSections.Movements)
                ? await _context.AccountPostings.AsNoTracking()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false)
                : [];
            var fees = normalized.Includes(DataSections.Movements)
                ? await _context.Set<FeeComponentRecord>().AsNoTracking()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false)
                : [];
            var observations = normalized.Includes(DataSections.PriceHistory)
                ? await _context.PriceObservations.AsNoTracking()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false)
                : [];
            var balances = normalized.Includes(DataSections.AccountHistory)
                ? await _context.ManualBalances.AsNoTracking()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false)
                : [];
            var loans = normalized.Includes(DataSections.AccountHistory)
                ? await _context.Loans.AsNoTracking().ToListAsync(cancellationToken)
                    .ConfigureAwait(false)
                : [];
            var readings = normalized.Includes(DataSections.AccountHistory)
                ? await _context.LoanReadings.AsNoTracking()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false)
                : [];

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return new BackupSnapshot(
                Accounts: accounts.Select(BackupRowMapper.ToBackup).ToArray(),
                Categories: categories.Select(BackupRowMapper.ToBackup).ToArray(),
                Instruments: instruments.Select(BackupRowMapper.ToBackup).ToArray(),
                FinancialOperations: operations.Select(BackupRowMapper.ToBackup).ToArray(),
                AccountPostings: postings.Select(BackupRowMapper.ToBackup).ToArray(),
                FeeComponents: fees.Select(BackupRowMapper.ToBackup).ToArray(),
                PriceObservations: observations.Select(BackupRowMapper.ToBackup).ToArray(),
                ManualBalances: balances.Select(BackupRowMapper.ToBackup).ToArray(),
                Loans: loans.Select(BackupRowMapper.ToBackup).ToArray(),
                LoanReadings: readings.Select(BackupRowMapper.ToBackup).ToArray());
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<RestoreReferenceCheck> CheckReferencesAsync(
        BackupSnapshot snapshot,
        DataSections sections,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var normalized = sections.Normalize();
        if (normalized == DataSections.None ||
            (normalized & DataSections.AllData) == DataSections.None)
        {
            return RestoreReferenceCheck.Valid;
        }

        var isFull = normalized.IsFullDataRestore();
        var accounts = isFull
            ? snapshot.Accounts.Select(account => account.Id).ToHashSet()
            : await _context.Accounts.AsNoTracking()
                .Select(account => account.Id)
                .ToHashSetAsync(cancellationToken)
                .ConfigureAwait(false);
        var categories = isFull
            ? snapshot.Categories.Select(category => category.Id).ToHashSet()
            : await _context.Categories.AsNoTracking()
                .Select(category => category.Id)
                .ToHashSetAsync(cancellationToken)
                .ConfigureAwait(false);
        var instruments = isFull
            ? snapshot.Instruments.Select(instrument => instrument.Id).ToHashSet()
            : await _context.Instruments.AsNoTracking()
                .Select(instrument => instrument.Id)
                .ToHashSetAsync(cancellationToken)
                .ConfigureAwait(false);

        if (!isFull)
        {
            if (normalized.Includes(DataSections.Accounts))
            {
                accounts.UnionWith(snapshot.Accounts.Select(account => account.Id));
            }

            if (normalized.Includes(DataSections.Categories))
            {
                categories.UnionWith(snapshot.Categories.Select(category => category.Id));
            }

            if (normalized.Includes(DataSections.Instruments))
            {
                instruments.UnionWith(snapshot.Instruments.Select(instrument => instrument.Id));
            }
        }

        var missingAccounts = new HashSet<Guid>();
        var missingCategories = new HashSet<Guid>();
        var missingInstruments = new HashSet<Guid>();
        var issues = new List<string>();

        void RequireAccount(Guid? id)
        {
            if (id is { } value && !accounts.Contains(value))
            {
                missingAccounts.Add(value);
            }
        }

        void RequireCategory(Guid? id)
        {
            if (id is { } value && !categories.Contains(value))
            {
                missingCategories.Add(value);
            }
        }

        void RequireInstrument(Guid? id)
        {
            if (id is { } value && !instruments.Contains(value))
            {
                missingInstruments.Add(value);
            }
        }

        if (normalized.Includes(DataSections.Movements))
        {
            foreach (var operation in snapshot.FinancialOperations)
            {
                RequireAccount(operation.AccountId);
                RequireAccount(operation.SourceAccountId);
                RequireAccount(operation.DestinationAccountId);
                RequireAccount(operation.SettlementAccountId);
                RequireCategory(operation.CategoryId);
                RequireInstrument(operation.InstrumentId);
            }

            foreach (var posting in snapshot.AccountPostings)
            {
                RequireAccount(posting.AccountId);
            }

            foreach (var fee in snapshot.FeeComponents)
            {
                RequireAccount(fee.DebitAccountId);
            }
        }

        if (normalized.Includes(DataSections.AccountHistory))
        {
            foreach (var balance in snapshot.ManualBalances)
            {
                RequireAccount(balance.AccountId);
            }

            foreach (var loan in snapshot.Loans)
            {
                RequireAccount(loan.AccountId);
            }

            var loanIds = snapshot.Loans.Select(loan => loan.Id).ToHashSet();
            if (snapshot.LoanReadings.Any(reading => !loanIds.Contains(reading.LoanId)))
            {
                issues.Add(
                    "Il backup contiene letture di prestiti senza il prestito corrispondente. " +
                    "Rigenera il backup includendo la sezione «Storico valore conti».");
            }
        }

        if (normalized.Includes(DataSections.PriceHistory))
        {
            foreach (var observation in snapshot.PriceObservations)
            {
                RequireInstrument(observation.InstrumentId);
            }
        }

        if (missingAccounts.Count > 0)
        {
            issues.Insert(
                0,
                $"Il backup fa riferimento a {missingAccounts.Count} conti non presenti nel database. " +
                "Includi la sezione «Conti» nel backup oppure importa prima i conti.");
        }

        if (missingCategories.Count > 0)
        {
            issues.Insert(
                0,
                $"Il backup fa riferimento a {missingCategories.Count} categorie non presenti nel database. " +
                "Includi la sezione «Categorie» nel backup oppure importa prima le categorie.");
        }

        if (missingInstruments.Count > 0)
        {
            issues.Insert(
                0,
                $"Il backup fa riferimento a {missingInstruments.Count} strumenti non presenti nel database. " +
                "Includi la sezione «Strumenti finanziari» nel backup oppure importa prima gli strumenti.");
        }

        return issues.Count == 0
            ? RestoreReferenceCheck.Valid
            : new RestoreReferenceCheck(false, issues);
    }

    public async Task RestoreAsync(
        BackupSnapshot snapshot,
        DataSections sections,
        DateTimeOffset restoredAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var normalized = sections.Normalize();
        if (normalized == DataSections.None)
        {
            throw new BackupValidationException("Il backup non contiene sezioni dati.");
        }

        var isFull = normalized.IsFullDataRestore();
        var ownsTransaction = _context.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            _context.ChangeTracker.Clear();

            if (isFull)
            {
                await _context.Database.ExecuteSqlRawAsync(TruncateAllDataSql, cancellationToken)
                    .ConfigureAwait(false);
                await InsertFullSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                if (normalized.Includes(DataSections.Accounts))
                {
                    await UpsertAccountsAsync(snapshot.Accounts, cancellationToken).ConfigureAwait(false);
                }

                if (normalized.Includes(DataSections.Categories))
                {
                    await UpsertCategoriesAsync(snapshot.Categories, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (normalized.Includes(DataSections.Instruments))
                {
                    await UpsertInstrumentsAsync(snapshot.Instruments, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (normalized.Includes(DataSections.AccountHistory))
                {
                    await _context.Database.ExecuteSqlRawAsync(
                            TruncateAccountHistorySql,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await InsertAccountHistoryAsync(snapshot, cancellationToken).ConfigureAwait(false);
                }

                if (normalized.Includes(DataSections.PriceHistory))
                {
                    await _context.Database.ExecuteSqlRawAsync(
                            TruncatePriceHistorySql,
                            cancellationToken)
                        .ConfigureAwait(false);
                    _context.PriceObservations.AddRange(
                        snapshot.PriceObservations.Select(BackupRowMapper.ToRecord));
                    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                if (normalized.Includes(DataSections.Movements))
                {
                    await _context.Database.ExecuteSqlRawAsync(
                            TruncateMovementsSql,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await InsertMovementsAsync(snapshot, cancellationToken).ConfigureAwait(false);
                }
            }

            var counts = await CountAsync(cancellationToken).ConfigureAwait(false);
            VerifyCounts(counts, snapshot.Counts, normalized, isFull);

            await UpsertMetadataAsync(
                    BuildSeedMetadata(restoredAt, AppMetadataKeys.LastRestoreAt),
                    cancellationToken)
                .ConfigureAwait(false);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }

            _context.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task DeleteAsync(
        DataDataset datasets,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken = default)
    {
        var tables = TablesForDeletion(datasets);
        if (tables.Count == 0)
        {
            return;
        }

        var ownsTransaction = _context.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            _context.ChangeTracker.Clear();
            var sql = "TRUNCATE TABLE " + string.Join(", ", tables) + " RESTART IDENTITY;";
            await _context.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);

            await UpsertMetadataAsync(
                    BuildSeedMetadata(deletedAt, AppMetadataKeys.LastDeletionAt),
                    cancellationToken)
                .ConfigureAwait(false);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }

            _context.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<IReadOnlyList<AppMetadataEntry>> ListMetadataAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await _context.AppMetadata
            .AsNoTracking()
            .OrderBy(metadata => metadata.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records
            .Select(record => new AppMetadataEntry(record.Key, record.Value, record.UpdatedAt))
            .ToArray();
    }

    private async Task InsertFullSnapshotAsync(
        BackupSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        _context.Accounts.AddRange(snapshot.Accounts.Select(BackupRowMapper.ToRecord));
        _context.Categories.AddRange(snapshot.Categories.Select(BackupRowMapper.ToRecord));
        _context.Instruments.AddRange(snapshot.Instruments.Select(BackupRowMapper.ToRecord));
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await InsertMovementsAsync(snapshot, cancellationToken).ConfigureAwait(false);
        await InsertAccountHistoryAsync(snapshot, cancellationToken).ConfigureAwait(false);

        _context.PriceObservations.AddRange(
            snapshot.PriceObservations.Select(BackupRowMapper.ToRecord));
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task InsertMovementsAsync(
        BackupSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var originals = snapshot.FinancialOperations
            .Where(operation => operation.Kind != OperationKind.Reversal)
            .Select(BackupRowMapper.ToRecord)
            .ToArray();
        if (originals.Length > 0)
        {
            _context.FinancialOperations.AddRange(originals);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var reversals = snapshot.FinancialOperations
            .Where(operation => operation.Kind == OperationKind.Reversal)
            .Select(BackupRowMapper.ToRecord)
            .ToArray();
        if (reversals.Length > 0)
        {
            _context.FinancialOperations.AddRange(reversals);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        _context.Set<FeeComponentRecord>()
            .AddRange(snapshot.FeeComponents.Select(BackupRowMapper.ToRecord));
        _context.AccountPostings.AddRange(snapshot.AccountPostings.Select(BackupRowMapper.ToRecord));
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task InsertAccountHistoryAsync(
        BackupSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        _context.ManualBalances.AddRange(snapshot.ManualBalances.Select(BackupRowMapper.ToRecord));
        _context.Loans.AddRange(snapshot.Loans.Select(BackupRowMapper.ToRecord));
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _context.LoanReadings.AddRange(snapshot.LoanReadings.Select(BackupRowMapper.ToRecord));
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task UpsertAccountsAsync(
        IReadOnlyList<AccountBackup> accounts,
        CancellationToken cancellationToken)
    {
        if (accounts.Count == 0)
        {
            return;
        }

        var ids = accounts.Select(account => account.Id).ToArray();
        var existing = await _context.Accounts
            .Where(account => ids.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, cancellationToken)
            .ConfigureAwait(false);

        foreach (var backup in accounts)
        {
            var record = BackupRowMapper.ToRecord(backup);
            if (existing.TryGetValue(backup.Id, out var current))
            {
                current.Name = record.Name;
                current.Type = record.Type;
                current.CurrencyCode = record.CurrencyCode;
                current.Status = record.Status;
                current.ColorHex = record.ColorHex;
                current.IconDataUrl = record.IconDataUrl;
            }
            else
            {
                _context.Accounts.Add(record);
            }
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task UpsertCategoriesAsync(
        IReadOnlyList<CategoryBackup> categories,
        CancellationToken cancellationToken)
    {
        if (categories.Count == 0)
        {
            return;
        }

        var ids = categories.Select(category => category.Id).ToArray();
        var existing = await _context.Categories
            .Where(category => ids.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, cancellationToken)
            .ConfigureAwait(false);

        foreach (var backup in categories)
        {
            var record = BackupRowMapper.ToRecord(backup);
            if (existing.TryGetValue(backup.Id, out var current))
            {
                current.Name = record.Name;
                current.Type = record.Type;
                current.Description = record.Description;
                current.Subcategory = record.Subcategory;
                current.Icon = record.Icon;
                current.ColorHex = record.ColorHex;
            }
            else
            {
                _context.Categories.Add(record);
            }
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task UpsertInstrumentsAsync(
        IReadOnlyList<InstrumentBackup> instruments,
        CancellationToken cancellationToken)
    {
        if (instruments.Count == 0)
        {
            return;
        }

        var ids = instruments.Select(instrument => instrument.Id).ToArray();
        var existing = await _context.Instruments
            .Where(instrument => ids.Contains(instrument.Id))
            .ToDictionaryAsync(instrument => instrument.Id, cancellationToken)
            .ConfigureAwait(false);

        foreach (var backup in instruments)
        {
            var record = BackupRowMapper.ToRecord(backup);
            if (existing.TryGetValue(backup.Id, out var current))
            {
                current.Name = record.Name;
                current.Symbol = record.Symbol;
                current.Isin = record.Isin;
                current.AssetClass = record.AssetClass;
                current.Status = record.Status;
                current.QuoteCurrencyCode = record.QuoteCurrencyCode;
                current.PriceUnit = record.PriceUnit;
                current.NotionalValueAmount = record.NotionalValueAmount;
                current.NotionalValueCurrencyCode = record.NotionalValueCurrencyCode;
                current.ReferenceUrl = record.ReferenceUrl;
                current.Notes = record.Notes;
                current.MaturityDate = record.MaturityDate;
                current.CouponRate = record.CouponRate;
            }
            else
            {
                _context.Instruments.Add(record);
            }
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void VerifyCounts(
        DataCounts actual,
        DataCounts expected,
        DataSections sections,
        bool isFull)
    {
        if (isFull)
        {
            if (actual with { AppMetadata = 0 } != expected)
            {
                throw new BackupValidationException(
                    "I dati ripristinati non corrispondono ai conteggi del backup. " +
                    "Operazione annullata.");
            }

            return;
        }

        if (sections.Includes(DataSections.Movements) &&
            (actual.FinancialOperations != expected.FinancialOperations ||
             actual.AccountPostings != expected.AccountPostings ||
             actual.FeeComponents != expected.FeeComponents))
        {
            throw CountMismatch();
        }

        if (sections.Includes(DataSections.AccountHistory) &&
            (actual.ManualBalances != expected.ManualBalances ||
             actual.Loans != expected.Loans ||
             actual.LoanReadings != expected.LoanReadings))
        {
            throw CountMismatch();
        }

        if (sections.Includes(DataSections.PriceHistory) &&
            actual.PriceObservations != expected.PriceObservations)
        {
            throw CountMismatch();
        }
    }

    private static BackupValidationException CountMismatch() => new(
        "I dati ripristinati non corrispondono ai conteggi del backup. Operazione annullata.");

    private static IReadOnlyList<string> TablesForDeletion(DataDataset datasets)
    {
        if (datasets == DataDataset.None)
        {
            return [];
        }

        if (datasets.Includes(DataDataset.Everything))
        {
            return
            [
                "\"Accounts\"",
                "\"Categories\"",
                "\"Instruments\"",
                "\"FinancialOperations\"",
                "\"AccountPostings\"",
                "\"InvestmentOperationFees\"",
                "\"PriceObservations\"",
                "\"ManualBalances\"",
                "\"Loans\"",
                "\"LoanReadings\"",
                "\"AppMetadata\"",
            ];
        }

        var tables = new List<string>(10);

        if (datasets.Includes(DataDataset.Movements))
        {
            tables.Add("\"FinancialOperations\"");
            tables.Add("\"AccountPostings\"");
            tables.Add("\"InvestmentOperationFees\"");
        }

        if (datasets.Includes(DataDataset.Instruments))
        {
            tables.Add("\"PriceObservations\"");
            tables.Add("\"Instruments\"");
        }

        if (datasets.Includes(DataDataset.Accounts))
        {
            tables.Add("\"ManualBalances\"");
            tables.Add("\"LoanReadings\"");
            tables.Add("\"Loans\"");
            tables.Add("\"Accounts\"");
        }

        if (datasets.Includes(DataDataset.Categories))
        {
            tables.Add("\"Categories\"");
        }

        return tables;
    }

    private static IReadOnlyDictionary<string, string> BuildSeedMetadata(
        DateTimeOffset timestamp,
        string auditKey)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [auditKey] = timestamp.ToString("O"),
        };

        foreach (var (key, value) in AppMetadataKeys.SeedMarkers)
        {
            values[key] = value;
        }

        return values;
    }

    private async Task UpsertMetadataAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        foreach (var (key, value) in values)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO "AppMetadata" ("Key", "Value", "UpdatedAt")
                     VALUES ({key}, {value}, {DateTimeOffset.UtcNow})
                     ON CONFLICT ("Key") DO UPDATE
                     SET "Value" = EXCLUDED."Value", "UpdatedAt" = EXCLUDED."UpdatedAt";
                     """,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
