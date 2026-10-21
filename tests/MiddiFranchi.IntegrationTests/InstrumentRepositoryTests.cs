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
            InstrumentType.Etf,
            Currency.Euro,
            PriceUnit.PerUnit,
            symbol: "V3MA");
        var withoutSymbol = new Instrument(
            Guid.NewGuid(),
            "ETF senza sigla",
            InstrumentType.Etf,
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
}
