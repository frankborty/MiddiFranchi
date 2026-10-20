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

    public async Task<IReadOnlyList<InvestmentOperation>> ListInvestmentsByPortfolioAsync(
        Guid portfolioId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var records = await _context.FinancialOperations
            .AsNoTracking()
            .Where(operation =>
                operation.Kind == OperationKind.Investment &&
                operation.PortfolioId == portfolioId &&
                operation.OperationDate <= asOfDate &&
                !_context.FinancialOperations.Any(reversal =>
                    reversal.Kind == OperationKind.Reversal &&
                    reversal.OriginalOperationId == operation.Id))
            .ToListAsync(cancellationToken);

        var operations = await FinancialOperationMaterializer.MaterializeAsync(_context, records, cancellationToken);
        return operations.OfType<InvestmentOperation>().ToArray();
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
}
