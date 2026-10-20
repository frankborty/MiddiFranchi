using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Infrastructure.Persistence.Repositories;

public sealed class ExchangeRateObservationRepository : IExchangeRateObservationRepository
{
    private readonly MiddiFranchiDbContext _context;

    public ExchangeRateObservationRepository(MiddiFranchiDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ExchangeRateObservation>> ListForCurrencyAsync(
        Currency currency,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        if (currency.Code is null)
        {
            throw new ArgumentException("La valuta è obbligatoria.", nameof(currency));
        }

        var records = await _context.ExchangeRateObservations
            .AsNoTracking()
            .Where(observation =>
                observation.RateCurrencyCode == currency.Code &&
                observation.ValidAt <= asOfDate)
            .OrderByDescending(observation => observation.ValidAt)
            .ThenByDescending(observation => observation.ObservedAt)
            .ThenByDescending(observation => observation.Source)
            .ThenByDescending(observation => observation.Id)
            .ToListAsync(cancellationToken);

        return records.Select(PersistenceMapper.ToDomain).ToArray();
    }

    public async Task AddAsync(ExchangeRateObservation observation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);

        _context.ExchangeRateObservations.Add(PersistenceMapper.ToRecord(observation));
        await _context.SaveChangesAsync(cancellationToken);
    }
}
