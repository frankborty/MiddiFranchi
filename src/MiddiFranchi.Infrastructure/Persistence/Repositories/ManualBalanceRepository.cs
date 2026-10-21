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
