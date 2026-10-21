using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.Persistence.Repositories;
using MiddiFranchi.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class ObservationRepositoryTests : PostgresTestBase
{
    public ObservationRepositoryTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task PriceObservations_ShouldRoundTripAndRespectValidAt()
    {
        var instrument = new Instrument(
            Guid.NewGuid(),
            "ETF",
            InstrumentType.Etf,
            Currency.Euro,
            PriceUnit.PerUnit);
        await new InstrumentRepository(Context).AddAsync(instrument);

        var older = PriceObservation.AtUnitPrice(
            Guid.NewGuid(),
            instrument,
            new UnitPrice(new Money(123.456789012345678m, Currency.Euro)),
            new DateOnly(2026, 3, 10),
            new DateTimeOffset(2026, 3, 10, 18, 0, 0, TimeSpan.Zero),
            "Fonte A");
        var newer = PriceObservation.AtUnitPrice(
            Guid.NewGuid(),
            instrument,
            new UnitPrice(new Money(130m, Currency.Euro)),
            new DateOnly(2026, 3, 12),
            new DateTimeOffset(2026, 3, 12, 18, 0, 0, TimeSpan.Zero),
            "Fonte B");
        var future = PriceObservation.AtUnitPrice(
            Guid.NewGuid(),
            instrument,
            new UnitPrice(new Money(140m, Currency.Euro)),
            new DateOnly(2026, 3, 20),
            new DateTimeOffset(2026, 3, 20, 18, 0, 0, TimeSpan.Zero),
            "Fonte C");
        var repository = new PriceObservationRepository(Context);

        await repository.AddAsync(older);
        await repository.AddAsync(newer);
        await repository.AddAsync(future);
        ClearTracker();

        var beforeNewer = await repository.ListForInstrumentAsync(instrument.Id, new DateOnly(2026, 3, 11));
        var beforeNewerObservation = beforeNewer.ShouldHaveSingleItem();
        beforeNewerObservation.Id.ShouldBe(older.Id);
        beforeNewerObservation.Price.ShouldBe(123.456789012345678m);
        beforeNewerObservation.PriceCurrency.ShouldBe(Currency.Euro);
        beforeNewerObservation.ValidAt.ShouldBe(new DateOnly(2026, 3, 10));
        beforeNewerObservation.ObservedAt.ShouldBe(older.ObservedAt);

        var asOfNewer = await repository.ListForInstrumentAsync(instrument.Id, new DateOnly(2026, 3, 12));
        asOfNewer.Count.ShouldBe(2);
        asOfNewer[0].Id.ShouldBe(newer.Id);
        asOfNewer[1].Id.ShouldBe(older.Id);
    }

    [PostgresFact]
    public async Task PercentOfNominalObservation_ShouldRoundTripWithoutCurrency()
    {
        var bond = new Instrument(
            Guid.NewGuid(),
            "Obbligazione",
            InstrumentType.Bond,
            Currency.Euro,
            PriceUnit.PercentOfNominal,
            new Money(1000m, Currency.Euro));
        await new InstrumentRepository(Context).AddAsync(bond);
        var observation = PriceObservation.AsPercentOfNominal(
            Guid.NewGuid(),
            bond,
            98.5m,
            new DateOnly(2026, 3, 15),
            new DateTimeOffset(2026, 3, 15, 18, 0, 0, TimeSpan.Zero),
            "Mercato");

        var repository = new PriceObservationRepository(Context);
        await repository.AddAsync(observation);
        ClearTracker();

        var loaded = (await repository.ListForInstrumentAsync(bond.Id, new DateOnly(2026, 3, 15)))
            .ShouldHaveSingleItem();
        loaded.Unit.ShouldBe(PriceUnit.PercentOfNominal);
        loaded.Price.ShouldBe(98.5m);
        loaded.PriceCurrency.ShouldBeNull();
        loaded.Instrument.NotionalValue.ShouldBe(new Money(1000m, Currency.Euro));
    }

}
