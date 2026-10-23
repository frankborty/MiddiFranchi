using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.Persistence.Repositories;
using MiddiFranchi.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class InstrumentRepositoryTests : PostgresTestBase
{
    public InstrumentRepositoryTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task AddAndGet_ShouldRoundTripTheOptionalSymbol()
    {
        var repository = new InstrumentRepository(Context);
        var withSymbol = new Instrument(
            Guid.NewGuid(),
            "ETF globale",
            AssetClass.Equity,
            Currency.Euro,
            PriceUnit.PerUnit,
            symbol: "V3MA");
        var withoutSymbol = new Instrument(
            Guid.NewGuid(),
            "ETF senza sigla",
            AssetClass.Equity,
            Currency.Euro,
            PriceUnit.PerUnit);

        await repository.AddAsync(withSymbol);
        await repository.AddAsync(withoutSymbol);
        ClearTracker();

        (await repository.GetByIdAsync(withSymbol.Id)).ShouldNotBeNull()
            .Symbol.ShouldBe("V3MA");
        (await repository.GetByIdAsync(withoutSymbol.Id)).ShouldNotBeNull()
            .Symbol.ShouldBeNull();
    }

    [PostgresFact]
    public async Task AddAndGet_ShouldRoundTripTheCatalogDetails()
    {
        var repository = new InstrumentRepository(Context);
        var instrument = new Instrument(
            Guid.NewGuid(),
            "BTP 2030",
            AssetClass.Bond,
            Currency.Euro,
            PriceUnit.PercentOfNominal,
            new Money(1000m, Currency.Euro),
            symbol: "BTP30",
            isin: "IT0001234567",
            referenceUrl: "https://example.com/btp",
            notes: "Cedola fissa",
            status: InstrumentStatus.Archived,
            maturityDate: new DateOnly(2030, 1, 1),
            couponRate: 3.5m);

        await repository.AddAsync(instrument);
        ClearTracker();

        var loaded = (await repository.GetByIdAsync(instrument.Id)).ShouldNotBeNull();
        loaded.AssetClass.ShouldBe(AssetClass.Bond);
        loaded.Status.ShouldBe(InstrumentStatus.Archived);
        loaded.Isin.ShouldBe("IT0001234567");
        loaded.ReferenceUrl.ShouldBe("https://example.com/btp");
        loaded.Notes.ShouldBe("Cedola fissa");
        loaded.MaturityDate.ShouldBe(new DateOnly(2030, 1, 1));
        loaded.CouponRate.ShouldBe(3.5m);
    }

    [PostgresFact]
    public async Task AddDuplicateIsin_ShouldBeRejectedByTheDatabase()
    {
        var repository = new InstrumentRepository(Context);
        var first = new Instrument(
            Guid.NewGuid(), "ETF 1", AssetClass.Equity, Currency.Euro, PriceUnit.PerUnit,
            isin: "IE00B4L5Y983");
        var second = new Instrument(
            Guid.NewGuid(), "ETF 2", AssetClass.Equity, Currency.Euro, PriceUnit.PerUnit,
            isin: "IE00B4L5Y983");

        await repository.AddAsync(first);
        ClearTracker();

        await Should.ThrowAsync<DbUpdateException>(() => repository.AddAsync(second));
    }

    [PostgresFact]
    public async Task IdentityQueries_ShouldBeCaseInsensitiveAndExcludeSelf()
    {
        var repository = new InstrumentRepository(Context);
        var instrument = new Instrument(
            Guid.NewGuid(), "ETF globale", AssetClass.Equity, Currency.Euro, PriceUnit.PerUnit,
            symbol: "V3MA", isin: "IE00B4L5Y983");
        await repository.AddAsync(instrument);

        (await repository.IsinExistsAsync("ie00b4l5y983", null)).ShouldBeTrue();
        (await repository.IsinExistsAsync("ie00b4l5y983", instrument.Id)).ShouldBeFalse();
        (await repository.SymbolExistsAsync("v3ma", null)).ShouldBeTrue();
        (await repository.SymbolExistsAsync("v3ma", instrument.Id)).ShouldBeFalse();
    }

    [PostgresFact]
    public async Task Update_ShouldPersistTheCatalogChanges()
    {
        var repository = new InstrumentRepository(Context);
        var instrument = new Instrument(
            Guid.NewGuid(), "ETF globale", AssetClass.Equity, Currency.Euro, PriceUnit.PerUnit);
        await repository.AddAsync(instrument);
        ClearTracker();

        var updated = new Instrument(
            instrument.Id,
            "ETF aggiornato",
            AssetClass.Mixed,
            Currency.Euro,
            PriceUnit.PerUnit,
            symbol: "NEW",
            isin: "IE00B4L5Y983",
            notes: "Nota",
            status: InstrumentStatus.Archived);
        await repository.UpdateAsync(updated);
        ClearTracker();

        var loaded = (await repository.GetByIdAsync(instrument.Id)).ShouldNotBeNull();
        loaded.Name.ShouldBe("ETF aggiornato");
        loaded.AssetClass.ShouldBe(AssetClass.Mixed);
        loaded.Symbol.ShouldBe("NEW");
        loaded.Isin.ShouldBe("IE00B4L5Y983");
        loaded.Notes.ShouldBe("Nota");
        loaded.Status.ShouldBe(InstrumentStatus.Archived);
    }

    [PostgresFact]
    public async Task Delete_ShouldRemoveTheInstrument()
    {
        var repository = new InstrumentRepository(Context);
        var instrument = new Instrument(
            Guid.NewGuid(), "ETF globale", AssetClass.Equity, Currency.Euro, PriceUnit.PerUnit);
        await repository.AddAsync(instrument);

        await repository.DeleteAsync(instrument.Id);
        ClearTracker();

        (await repository.GetByIdAsync(instrument.Id)).ShouldBeNull();
    }
}
