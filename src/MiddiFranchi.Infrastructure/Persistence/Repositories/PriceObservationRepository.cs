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

        if (records.Count == 0)
        {
            return [];
        }

        var instrumentRecord = await _context.Instruments
            .AsNoTracking()
            .SingleOrDefaultAsync(instrument => instrument.Id == instrumentId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Lo strumento {instrumentId} delle quotazioni non esiste.");

        var instrument = PersistenceMapper.ToDomain(instrumentRecord);
        return records.Select(record => PersistenceMapper.ToDomain(record, instrument)).ToArray();
    }

    public async Task AddAsync(PriceObservation observation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);

        _context.PriceObservations.Add(PersistenceMapper.ToRecord(observation));
        await _context.SaveChangesAsync(cancellationToken);
    }
}
