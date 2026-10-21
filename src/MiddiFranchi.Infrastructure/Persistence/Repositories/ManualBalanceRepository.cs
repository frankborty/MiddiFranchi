using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Repositories;

public sealed class ManualBalanceRepository : IManualBalanceRepository
{
    private readonly MiddiFranchiDbContext _context;

    public ManualBalanceRepository(MiddiFranchiDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ManualBalance reading, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        _context.ManualBalances.Add(PersistenceMapper.ToRecord(reading));
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ManualBalance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _context.ManualBalances
            .AsNoTracking()
            .SingleOrDefaultAsync(reading => reading.Id == id, cancellationToken);

        return record is null ? null : await ToDomainAsync(record, cancellationToken);
    }

    public async Task<ManualBalance?> GetLatestAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var record = await _context.ManualBalances
            .AsNoTracking()
            .Where(reading => reading.AccountId == accountId)
            .OrderByDescending(reading => reading.RecordedAt)
            .ThenByDescending(reading => reading.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return record is null ? null : await ToDomainAsync(record, cancellationToken);
    }

    public async Task<ManualBalance?> GetLatestOnOrBeforeAsync(
        Guid accountId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var record = await _context.ManualBalances
            .AsNoTracking()
            .Where(reading => reading.AccountId == accountId && reading.ValidAt <= asOfDate)
            .OrderByDescending(reading => reading.ValidAt)
            .ThenByDescending(reading => reading.RecordedAt)
            .ThenByDescending(reading => reading.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return record is null ? null : await ToDomainAsync(record, cancellationToken);
    }

    public async Task<IReadOnlyList<ManualBalance>> ListForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var records = await _context.ManualBalances
            .AsNoTracking()
            .Where(reading => reading.AccountId == accountId)
            .OrderBy(reading => reading.ValidAt)
            .ThenBy(reading => reading.RecordedAt)
            .ThenBy(reading => reading.Id)
            .ToListAsync(cancellationToken);

        return await MaterializeAsync(records, cancellationToken);
    }

    public async Task<IReadOnlyList<ManualBalance>> ListAsync(CancellationToken cancellationToken = default)
    {
        var records = await _context.ManualBalances
            .AsNoTracking()
            .OrderBy(reading => reading.ValidAt)
            .ThenBy(reading => reading.RecordedAt)
            .ThenBy(reading => reading.Id)
            .ToListAsync(cancellationToken);

        return await MaterializeAsync(records, cancellationToken);
    }

    public async Task UpdateAsync(ManualBalance reading, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var record = await _context.ManualBalances
            .SingleOrDefaultAsync(candidate => candidate.Id == reading.Id, cancellationToken)
            ?? throw new InvalidOperationException($"La rilevazione {reading.Id} non esiste.");

        record.AmountAmount = reading.Amount.Amount;
        record.AmountCurrencyCode = reading.Amount.Currency.Code;
        record.ValidAt = reading.ValidAt;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _context.ManualBalances
            .SingleOrDefaultAsync(reading => reading.Id == id, cancellationToken);
        if (record is null)
        {
            return;
        }

        _context.ManualBalances.Remove(record);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceForAccountAsync(
        Guid accountId,
        IReadOnlyList<ManualBalance> readings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(readings);

        if (readings.Any(reading => reading.AccountId != accountId))
        {
            throw new ArgumentException(
                "Tutte le rilevazioni sostitutive devono appartenere al conto indicato.",
                nameof(readings));
        }

        var existing = await _context.ManualBalances
            .Where(reading => reading.AccountId == accountId)
            .ToListAsync(cancellationToken);

        var ownsTransaction = _context.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            _context.ManualBalances.RemoveRange(existing);
            foreach (var reading in readings)
            {
                _context.ManualBalances.Add(PersistenceMapper.ToRecord(reading));
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<IReadOnlyList<ManualBalance>> MaterializeAsync(
        List<ManualBalanceRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return [];
        }

        var accounts = await LoadAccountsByIdAsync(
            records.Select(record => record.AccountId).Distinct().ToArray(), cancellationToken);
        return records
            .Select(record => PersistenceMapper.ToDomain(record, accounts))
            .ToArray();
    }

    private async Task<ManualBalance> ToDomainAsync(ManualBalanceRecord record, CancellationToken cancellationToken)
    {
        var accounts = await LoadAccountsByIdAsync([record.AccountId], cancellationToken);
        return PersistenceMapper.ToDomain(record, accounts);
    }

    private async Task<IReadOnlyDictionary<Guid, Account>> LoadAccountsByIdAsync(
        Guid[] accountIds,
        CancellationToken cancellationToken)
    {
        var accountRecords = await _context.Accounts
            .AsNoTracking()
            .Where(account => accountIds.Contains(account.Id))
            .ToListAsync(cancellationToken);

        return accountRecords
            .Select(PersistenceMapper.ToDomain)
            .ToDictionary(account => account.Id);
    }
}
