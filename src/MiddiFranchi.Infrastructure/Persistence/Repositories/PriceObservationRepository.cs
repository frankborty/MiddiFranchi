using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Repositories;

public sealed class PriceObservationRepository : IPriceObservationRepository
{
    private readonly MiddiFranchiDbContext _context;

    public PriceObservationRepository(MiddiFranchiDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PriceObservation>> ListForInstrumentAsync(
        Guid instrumentId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var records = await _context.PriceObservations
            .AsNoTracking()
            .Where(observation =>
                observation.InstrumentId == instrumentId &&
                observation.ValidAt <= asOfDate)
            .OrderByDescending(observation => observation.ValidAt)
            .ThenByDescending(observation => observation.ObservedAt)
            .ThenByDescending(observation => observation.Source)
            .ThenByDescending(observation => observation.Id)
            .ToListAsync(cancellationToken);

        return await MaterializeAsync(records, cancellationToken);
    }

    public async Task<IReadOnlyList<PriceObservation>> ListAsync(CancellationToken cancellationToken = default)
    {
        var records = await _context.PriceObservations
            .AsNoTracking()
            .OrderBy(observation => observation.ValidAt)
            .ThenBy(observation => observation.ObservedAt)
            .ThenBy(observation => observation.Source)
            .ThenBy(observation => observation.Id)
            .ToListAsync(cancellationToken);

        return await MaterializeAsync(records, cancellationToken);
    }

    private async Task<IReadOnlyList<PriceObservation>> MaterializeAsync(
        List<PriceObservationRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return [];
        }

        var instrumentIds = records.Select(record => record.InstrumentId).Distinct().ToArray();
        var instruments = await _context.Instruments
            .AsNoTracking()
            .Where(instrument => instrumentIds.Contains(instrument.Id))
            .ToListAsync(cancellationToken)
            ?? throw new InvalidOperationException("Gli strumenti delle quotazioni non esistono.");

        if (instruments.Count != instrumentIds.Length)
        {
            throw new InvalidOperationException(
                $"Gli strumenti delle quotazioni richieste ({instrumentIds.Length}) non coincidono con quelli caricati ({instruments.Count}).");
        }

        var instrumentsById = instruments
            .ToDictionary(instrumentRecord => instrumentRecord.Id, PersistenceMapper.ToDomain);
        return records.Select(record => PersistenceMapper.ToDomain(record, instrumentsById[record.InstrumentId])).ToArray();
    }

    public Task<bool> AnyForInstrumentAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default) =>
        _context.PriceObservations.AnyAsync(
            observation => observation.InstrumentId == instrumentId,
            cancellationToken);

    public async Task AddAsync(PriceObservation observation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);

        _context.PriceObservations.Add(PersistenceMapper.ToRecord(observation));
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        PriceObservation observation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var record = await _context.PriceObservations
            .SingleOrDefaultAsync(candidate => candidate.Id == observation.Id, cancellationToken)
            ?? throw new InvalidOperationException($"La quotazione {observation.Id} non esiste.");

        record.Unit = observation.Unit;
        record.Price = observation.Price;
        record.PriceCurrencyCode = observation.PriceCurrency?.Code;
        record.ValidAt = observation.ValidAt;
        record.ObservedAt = observation.ObservedAt;
        record.TradeTime = observation.TradeTime;
        record.Source = observation.Source;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
