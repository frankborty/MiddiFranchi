using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Repositories;

public sealed class LoanRepository : ILoanRepository
{
    private readonly MiddiFranchiDbContext _context;

    public LoanRepository(MiddiFranchiDbContext context)
    {
        _context = context;
    }

    public async Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _context.Loans
            .AsNoTracking()
            .SingleOrDefaultAsync(loan => loan.Id == id, cancellationToken);

        return record is null
            ? null
            : (await MaterializeAsync([record], cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<Loan>> ListAsync(CancellationToken cancellationToken = default)
    {
        var records = await _context.Loans
            .AsNoTracking()
            .OrderBy(loan => loan.Borrower)
            .ThenBy(loan => loan.Id)
            .ToListAsync(cancellationToken);

        return await MaterializeAsync(records, cancellationToken);
    }

    public async Task<IReadOnlyList<Loan>> ListForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var records = await _context.Loans
            .AsNoTracking()
            .Where(loan => loan.AccountId == accountId)
            .OrderBy(loan => loan.Borrower)
            .ThenBy(loan => loan.Id)
            .ToListAsync(cancellationToken);

        return await MaterializeAsync(records, cancellationToken);
    }

    public async Task AddAsync(Loan loan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loan);

        _context.Loans.Add(PersistenceMapper.ToRecord(loan));
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid loanId, CancellationToken cancellationToken = default)
    {
        var loan = await _context.Loans
            .SingleOrDefaultAsync(record => record.Id == loanId, cancellationToken);
        if (loan is null)
        {
            return;
        }

        var readings = await _context.LoanReadings
            .Where(reading => reading.LoanId == loanId)
            .ToListAsync(cancellationToken);

        var ownsTransaction = _context.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            _context.LoanReadings.RemoveRange(readings);
            await _context.SaveChangesAsync(cancellationToken);

            _context.Loans.Remove(loan);
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

    private async Task<IReadOnlyList<Loan>> MaterializeAsync(
        List<LoanRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return [];
        }

        var accountIds = records.Select(record => record.AccountId).Distinct().ToArray();
        var accountRecords = await _context.Accounts
            .AsNoTracking()
            .Where(account => accountIds.Contains(account.Id))
            .ToListAsync(cancellationToken);
        var accounts = accountRecords
            .Select(PersistenceMapper.ToDomain)
            .ToDictionary(account => account.Id);

        return records
            .Select(record => PersistenceMapper.ToDomain(
                record,
                accounts.TryGetValue(record.AccountId, out var account)
                    ? account
                    : throw new InvalidOperationException(
                        $"Il conto del prestito {record.Id} non è stato caricato.")))
            .ToArray();
    }
}

public sealed class LoanReadingRepository : ILoanReadingRepository
{
    private readonly MiddiFranchiDbContext _context;

    public LoanReadingRepository(MiddiFranchiDbContext context)
    {
        _context = context;
    }

    public async Task<LoanReading?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _context.LoanReadings
            .AsNoTracking()
            .SingleOrDefaultAsync(reading => reading.Id == id, cancellationToken);

        return record is null
            ? null
            : (await MaterializeAsync([record], cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<LoanReading>> ListAsync(CancellationToken cancellationToken = default)
    {
        var records = await _context.LoanReadings
            .AsNoTracking()
            .OrderBy(reading => reading.ValidAt)
            .ThenBy(reading => reading.RecordedAt)
            .ThenBy(reading => reading.Id)
            .ToListAsync(cancellationToken);

        return await MaterializeAsync(records, cancellationToken);
    }

    public async Task<IReadOnlyList<LoanReading>> ListForLoanAsync(
        Guid loanId,
        CancellationToken cancellationToken = default)
    {
        var records = await _context.LoanReadings
            .AsNoTracking()
            .Where(reading => reading.LoanId == loanId)
            .OrderBy(reading => reading.ValidAt)
            .ThenBy(reading => reading.RecordedAt)
            .ThenBy(reading => reading.Id)
            .ToListAsync(cancellationToken);

        return await MaterializeAsync(records, cancellationToken);
    }

    public async Task AddAsync(LoanReading reading, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        _context.LoanReadings.Add(PersistenceMapper.ToRecord(reading));
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(LoanReading reading, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var record = await _context.LoanReadings
            .SingleOrDefaultAsync(candidate => candidate.Id == reading.Id, cancellationToken)
            ?? throw new InvalidOperationException($"La rilevazione {reading.Id} non esiste.");

        record.AmountAmount = reading.Amount.Amount;
        record.AmountCurrencyCode = reading.Amount.Currency.Code;
        record.ValidAt = reading.ValidAt;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _context.LoanReadings
            .SingleOrDefaultAsync(reading => reading.Id == id, cancellationToken);
        if (record is null)
        {
            return;
        }

        _context.LoanReadings.Remove(record);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<LoanReading>> MaterializeAsync(
        List<LoanReadingRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return [];
        }

        var loanIds = records.Select(record => record.LoanId).Distinct().ToArray();
        var loanRecords = await _context.Loans
            .AsNoTracking()
            .Where(loan => loanIds.Contains(loan.Id))
            .ToListAsync(cancellationToken);

        var accountIds = loanRecords.Select(loan => loan.AccountId).Distinct().ToArray();
        var accountRecords = await _context.Accounts
            .AsNoTracking()
            .Where(account => accountIds.Contains(account.Id))
            .ToListAsync(cancellationToken);
        var accounts = accountRecords
            .Select(PersistenceMapper.ToDomain)
            .ToDictionary(account => account.Id);

        var loans = loanRecords.ToDictionary(
            loan => loan.Id,
            loan => PersistenceMapper.ToDomain(
                loan,
                accounts.TryGetValue(loan.AccountId, out var account)
                    ? account
                    : throw new InvalidOperationException(
                        $"Il conto del prestito {loan.Id} non è stato caricato.")));

        return records
            .Select(record => PersistenceMapper.ToDomain(
                record,
                loans.TryGetValue(record.LoanId, out var loan)
                    ? loan
                    : throw new InvalidOperationException(
                        $"Il prestito della rilevazione {record.Id} non è stato caricato.")))
            .ToArray();
    }
}
