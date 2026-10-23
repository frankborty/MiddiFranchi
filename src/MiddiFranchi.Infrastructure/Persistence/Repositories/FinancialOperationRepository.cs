using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Repositories;

public sealed class FinancialOperationRepository : IFinancialOperationRepository
{
    private readonly MiddiFranchiDbContext _context;

    public FinancialOperationRepository(MiddiFranchiDbContext context)
    {
        _context = context;
    }

    public async Task<FinancialOperation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var target = await _context.FinancialOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(operation => operation.Id == id, cancellationToken);

        if (target is null)
        {
            return null;
        }

        var records = new List<FinancialOperationRecord> { target };

        if (target.OriginalOperationId is not null)
        {
            var original = await _context.FinancialOperations
                .AsNoTracking()
                .SingleOrDefaultAsync(operation => operation.Id == target.OriginalOperationId, cancellationToken);

            if (original is not null)
            {
                records.Add(original);
            }
        }

        var reversal = await _context.FinancialOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(operation => operation.OriginalOperationId == id, cancellationToken);

        if (reversal is not null)
        {
            records.Add(reversal);
        }

        var operations = await FinancialOperationMaterializer.MaterializeAsync(_context, records, cancellationToken);
        return operations.SingleOrDefault(operation => operation.Id == id);
    }

    public async Task<IReadOnlyList<FinancialOperation>> ListAsync(CancellationToken cancellationToken = default)
    {
        var records = await _context.FinancialOperations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return await FinancialOperationMaterializer.MaterializeAsync(_context, records, cancellationToken);
    }

    public async Task AddAsync(FinancialOperation operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.Status != OperationStatus.Registered)
        {
            throw new InvalidOperationException(
                "Non è possibile inserire un'operazione già stornata: lo storno è una registrazione separata.");
        }

        var record = PersistenceMapper.ToRecord(operation);

        if (operation is ReversalOperation reversal)
        {
            if (reversal.OriginalOperationId is null)
            {
                throw new InvalidOperationException("Lo storno non indica l'operazione originale.");
            }

            var original = await _context.FinancialOperations
                .SingleOrDefaultAsync(
                    candidate => candidate.Id == reversal.OriginalOperationId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"L'operazione originale {reversal.OriginalOperationId} non esiste.");

            if (original.Kind == OperationKind.Reversal)
            {
                throw new InvalidOperationException(
                    "Un'operazione di storno non può essere stornata.");
            }

            var alreadyReversed = await _context.FinancialOperations
                .AnyAsync(
                    candidate =>
                        candidate.Kind == OperationKind.Reversal &&
                        candidate.OriginalOperationId == reversal.OriginalOperationId,
                    cancellationToken);

            if (alreadyReversed)
            {
                throw new InvalidOperationException(
                    $"L'operazione originale {reversal.OriginalOperationId} risulta già stornata.");
            }
        }
        else
        {
            foreach (var posting in PersistenceMapper.ToPostingRecords(operation))
            {
                record.Postings.Add(posting);
            }
        }

        _context.FinancialOperations.Add(record);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(FinancialOperation operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var record = await _context.FinancialOperations
            .SingleOrDefaultAsync(candidate => candidate.Id == operation.Id, cancellationToken)
            ?? throw new InvalidOperationException("Operazione non trovata.");

        if (record.Kind is not (OperationKind.Income or OperationKind.Expense))
        {
            throw new InvalidOperationException("Solo le entrate e le uscite possono essere modificate.");
        }

        switch (operation)
        {
            case Income income:
                record.CategoryId = income.Category.Id;
                record.AmountAmount = income.Amount.Value.Amount;
                record.AmountCurrencyCode = income.Amount.Value.Currency.Code;
                break;
            case Expense expense:
                record.CategoryId = expense.Category.Id;
                record.AmountAmount = expense.Amount.Value.Amount;
                record.AmountCurrencyCode = expense.Amount.Value.Currency.Code;
                break;
            default:
                throw new InvalidOperationException("Solo le entrate e le uscite possono essere modificate.");
        }

        record.OperationDate = operation.OperationDate;
        record.Description = operation.Description;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateInvestmentAsync(
        InvestmentOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var record = await _context.FinancialOperations
            .SingleOrDefaultAsync(candidate => candidate.Id == operation.Id, cancellationToken)
            ?? throw new InvalidOperationException("Operazione non trovata.");

        if (record.Kind != OperationKind.Investment)
        {
            throw new InvalidOperationException("L'operazione non è un'operazione d'investimento.");
        }

        var alreadyReversed = await _context.FinancialOperations.AnyAsync(
            candidate =>
                candidate.Kind == OperationKind.Reversal &&
                candidate.OriginalOperationId == operation.Id,
            cancellationToken);

        if (alreadyReversed)
        {
            throw new InvalidOperationException(
                "L'operazione è stornata e non può essere modificata.");
        }

        var ownsTransaction = _context.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            await _context.AccountPostings
                .Where(posting => posting.FinancialOperationId == operation.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Set<FeeComponentRecord>()
                .Where(fee => fee.InvestmentOperationId == operation.Id)
                .ExecuteDeleteAsync(cancellationToken);

            record.OperationDate = operation.OperationDate;
            record.Description = operation.Description;
            record.InstrumentId = operation.Instrument?.Id;
            record.SettlementAccountId = operation.SettlementAccount.Id;
            record.QuantityValue = operation.Quantity?.Value;
            record.QuotedPriceUnit = operation.QuotedPrice?.Unit;
            record.QuotedPricePriceAmount = operation.QuotedPrice?.PricePerUnit?.Price.Amount;
            record.QuotedPricePriceCurrencyCode = operation.QuotedPrice?.PricePerUnit?.Price.Currency.Code;
            record.QuotedPricePercentOfNominal = operation.QuotedPrice?.PercentOfNominal;
            record.AmountAmount = operation.Amount?.Value.Amount;
            record.AmountCurrencyCode = operation.Amount?.Value.Currency.Code;

            _context.AccountPostings.AddRange(PersistenceMapper.ToPostingRecords(operation));

            var ordinal = 0;
            _context.Set<FeeComponentRecord>().AddRange(operation.Fees.Select(fee =>
                new FeeComponentRecord
                {
                    InvestmentOperationId = operation.Id,
                    Ordinal = ordinal++,
                    Kind = fee.Kind,
                    AmountAmount = fee.Amount.Value.Amount,
                    AmountCurrencyCode = fee.Amount.Value.Currency.Code,
                    DebitAccountId = fee.DebitAccount.Id,
                }));

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

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var operation = await _context.FinancialOperations
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (operation is null)
        {
            return;
        }

        var reversal = await _context.FinancialOperations
            .SingleOrDefaultAsync(candidate => candidate.OriginalOperationId == id, cancellationToken);

        var operationIds = reversal is null
            ? new[] { id }
            : new[] { id, reversal.Id };

        var ownsTransaction = _context.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            // Posting e commissioni hanno una FK Restrict verso l'operazione: vanno rimossi per primi.
            var postings = await _context.AccountPostings
                .Where(posting => operationIds.Contains(posting.FinancialOperationId))
                .ToListAsync(cancellationToken);
            var fees = await _context.Set<FeeComponentRecord>()
                .Where(fee => operationIds.Contains(fee.InvestmentOperationId))
                .ToListAsync(cancellationToken);
            _context.Set<FeeComponentRecord>().RemoveRange(fees);
            _context.AccountPostings.RemoveRange(postings);
            await _context.SaveChangesAsync(cancellationToken);

            if (reversal is not null)
            {
                _context.FinancialOperations.Remove(reversal);
            }

            _context.FinancialOperations.Remove(operation);
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

    public Task<bool> AnyReferencingAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        _context.FinancialOperations.AnyAsync(
            operation =>
                operation.AccountId == accountId ||
                operation.SourceAccountId == accountId ||
                operation.DestinationAccountId == accountId ||
                operation.SettlementAccountId == accountId ||
                operation.Fees.Any(fee => fee.DebitAccountId == accountId),
            cancellationToken);

    public Task<bool> AnyForInstrumentAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default) =>
        _context.FinancialOperations.AnyAsync(
            operation =>
                operation.Kind == OperationKind.Investment &&
                operation.InstrumentId == instrumentId,
            cancellationToken);
}
