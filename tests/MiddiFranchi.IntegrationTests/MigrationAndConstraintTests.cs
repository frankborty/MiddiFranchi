using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.Persistence.Entities;
using MiddiFranchi.Infrastructure.Persistence.Repositories;
using MiddiFranchi.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class MigrationAndConstraintTests : PostgresTestBase
{
    public MigrationAndConstraintTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task Database_ShouldBeReachableAndHaveTheInitialMigration()
    {
        (await Context.Database.CanConnectAsync()).ShouldBeTrue();

        var migrations = await Context.Database.GetAppliedMigrationsAsync();
        migrations.ShouldContain(migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }

    [PostgresFact]
    public async Task Database_ShouldAcceptAllCurrenciesSupportedByTheDomain()
    {
        var codes = new[] { "EUR", "USD", "GBP", "JPY" };

        foreach (var code in codes)
        {
            Context.Accounts.Add(new AccountRecord
            {
                Id = Guid.NewGuid(),
                Name = "Conto " + code,
                Type = AccountType.Checking,
                CurrencyCode = code,
                Status = AccountStatus.Active,
            });
        }

        await Context.SaveChangesAsync();

        (await Context.Accounts.CountAsync()).ShouldBe(codes.Length);
    }

    [PostgresFact]
    public async Task Database_ShouldRejectCurrencyCodesOutsideTheDomain()
    {
        Context.Accounts.Add(new AccountRecord
        {
            Id = Guid.NewGuid(),
            Name = "Conto non valido",
            Type = AccountType.Checking,
            CurrencyCode = "CHF",
            Status = AccountStatus.Active,
        });

        await Should.ThrowAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Database_ShouldRejectPostingSignsIncompatibleWithThePostingKind()
    {
        var source = new AccountRecord
        {
            Id = Guid.NewGuid(),
            Name = "Origine",
            Type = AccountType.Checking,
            CurrencyCode = "EUR",
            Status = AccountStatus.Active,
        };
        var destination = new AccountRecord
        {
            Id = Guid.NewGuid(),
            Name = "Destinazione",
            Type = AccountType.Checking,
            CurrencyCode = "EUR",
            Status = AccountStatus.Active,
        };
        Context.Accounts.AddRange(source, destination);

        var operation = new FinancialOperationRecord
        {
            Id = Guid.NewGuid(),
            Kind = OperationKind.Transfer,
            OperationDate = new DateOnly(2026, 3, 12),
            RecordedAt = new DateTimeOffset(2026, 3, 12, 12, 0, 0, TimeSpan.Zero),
            SourceAccountId = source.Id,
            DestinationAccountId = destination.Id,
            SourceAmountAmount = 10m,
            SourceAmountCurrencyCode = "EUR",
            DestinationAmountAmount = 10m,
            DestinationAmountCurrencyCode = "EUR",
        };
        Context.FinancialOperations.Add(operation);
        await Context.SaveChangesAsync();

        Context.AccountPostings.Add(new AccountPostingRecord
        {
            FinancialOperationId = operation.Id,
            AccountId = source.Id,
            Kind = PostingKind.Income,
            CurrencyCode = "EUR",
            SignedAmount = -1m,
        });

        await Should.ThrowAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Database_ShouldRejectOperationFieldsThatDoNotBelongToTheKind()
    {
        var (account, category) = await AddOrdinaryContextAsync();
        var income = await AddIncomeAsync(account, category);
        ClearTracker();

        Context.FinancialOperations.Add(new FinancialOperationRecord
        {
            Id = Guid.NewGuid(),
            Kind = OperationKind.Reversal,
            OperationDate = new DateOnly(2026, 3, 24),
            RecordedAt = new DateTimeOffset(2026, 3, 24, 8, 0, 0, TimeSpan.Zero),
            OriginalOperationId = income.Id,
            AmountAmount = 1m,
            AmountCurrencyCode = "EUR",
        });

        await Should.ThrowAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Database_ShouldRejectFeesAttachedToDividendOperations()
    {
        var account = new Account(
            Guid.NewGuid(), "Conto titoli", AccountType.Checking, Currency.Parse("EUR"), AccountStatus.Active);
        var portfolio = new InvestmentPortfolio(Guid.NewGuid(), "Portafoglio", Currency.Parse("EUR"));
        var instrument = new Instrument(
            Guid.NewGuid(), "ETF", InstrumentType.Etf, Currency.Parse("EUR"), PriceUnit.PerUnit);
        await new AccountRepository(Context).AddAsync(account);
        await new InvestmentPortfolioRepository(Context).AddAsync(portfolio);
        await new InstrumentRepository(Context).AddAsync(instrument);

        var dividend = InvestmentOperation.Dividend(
            Guid.NewGuid(),
            portfolio,
            instrument,
            account,
            new PositiveMoney(new Money(12.5m, Currency.Parse("EUR"))),
            new DateOnly(2026, 3, 25),
            new DateTimeOffset(2026, 3, 25, 8, 0, 0, TimeSpan.Zero));
        await new FinancialOperationRepository(Context).AddAsync(dividend);
        ClearTracker();

        Context.Set<FeeComponentRecord>().Add(new FeeComponentRecord
        {
            InvestmentOperationId = dividend.Id,
            Ordinal = 0,
            Kind = FeeKind.Standalone,
            AmountAmount = 0.5m,
            AmountCurrencyCode = "EUR",
            DebitAccountId = account.Id,
        });

        await Should.ThrowAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Database_ShouldPreventDeletingAReversal()
    {
        var (account, category) = await AddOrdinaryContextAsync();
        var income = await AddIncomeAsync(account, category);
        var reversal = new ReversalOperation(
            Guid.NewGuid(),
            income,
            new DateOnly(2026, 3, 26),
            new DateTimeOffset(2026, 3, 26, 8, 0, 0, TimeSpan.Zero));
        await new FinancialOperationRepository(Context).AddAsync(reversal);
        ClearTracker();

        var reversalRecord = await Context.FinancialOperations
            .SingleAsync(operation => operation.Id == reversal.Id);
        Context.FinancialOperations.Remove(reversalRecord);

        await Should.ThrowAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Database_ShouldPreventDeletingPostings()
    {
        var (account, category) = await AddOrdinaryContextAsync();
        var income = await AddIncomeAsync(account, category);
        ClearTracker();

        var posting = await Context.AccountPostings
            .SingleAsync(candidate => candidate.FinancialOperationId == income.Id);
        Context.AccountPostings.Remove(posting);

        await Should.ThrowAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Database_ShouldRejectNotionalValueOnPerUnitInstrument()
    {
        Context.Instruments.Add(new InstrumentRecord
        {
            Id = Guid.NewGuid(),
            Name = "ETF non valido",
            Type = InstrumentType.Etf,
            QuoteCurrencyCode = "EUR",
            PriceUnit = PriceUnit.PerUnit,
            NotionalValueAmount = 1000m,
            NotionalValueCurrencyCode = "EUR",
        });

        await Should.ThrowAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Database_ShouldRejectEurExchangeRateObservation()
    {
        Context.ExchangeRateObservations.Add(new ExchangeRateObservationRecord
        {
            Id = Guid.NewGuid(),
            RateCurrencyCode = "EUR",
            Rate = 1m,
            ValidAt = new DateOnly(2026, 3, 27),
            ObservedAt = new DateTimeOffset(2026, 3, 27, 8, 0, 0, TimeSpan.Zero),
            Source = "test",
        });

        await Should.ThrowAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Database_ShouldRejectTransferWithoutRateAcrossCurrencies()
    {
        var source = new AccountRecord
        {
            Id = Guid.NewGuid(),
            Name = "Origine EUR",
            Type = AccountType.Checking,
            CurrencyCode = "EUR",
            Status = AccountStatus.Active,
        };
        var destination = new AccountRecord
        {
            Id = Guid.NewGuid(),
            Name = "Destinazione USD",
            Type = AccountType.Checking,
            CurrencyCode = "USD",
            Status = AccountStatus.Active,
        };
        Context.Accounts.AddRange(source, destination);

        Context.FinancialOperations.Add(new FinancialOperationRecord
        {
            Id = Guid.NewGuid(),
            Kind = OperationKind.Transfer,
            OperationDate = new DateOnly(2026, 3, 28),
            RecordedAt = new DateTimeOffset(2026, 3, 28, 8, 0, 0, TimeSpan.Zero),
            SourceAccountId = source.Id,
            DestinationAccountId = destination.Id,
            SourceAmountAmount = 100m,
            SourceAmountCurrencyCode = "EUR",
            DestinationAmountAmount = 108m,
            DestinationAmountCurrencyCode = "USD",
        });

        await Should.ThrowAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Database_ShouldRejectTransferToTheSameAccount()
    {
        var account = new AccountRecord
        {
            Id = Guid.NewGuid(),
            Name = "Conto",
            Type = AccountType.Checking,
            CurrencyCode = "EUR",
            Status = AccountStatus.Active,
        };
        Context.Accounts.Add(account);

        Context.FinancialOperations.Add(new FinancialOperationRecord
        {
            Id = Guid.NewGuid(),
            Kind = OperationKind.Transfer,
            OperationDate = new DateOnly(2026, 3, 29),
            RecordedAt = new DateTimeOffset(2026, 3, 29, 8, 0, 0, TimeSpan.Zero),
            SourceAccountId = account.Id,
            DestinationAccountId = account.Id,
            SourceAmountAmount = 10m,
            SourceAmountCurrencyCode = "EUR",
            DestinationAmountAmount = 10m,
            DestinationAmountCurrencyCode = "EUR",
        });

        await Should.ThrowAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Database_ShouldRejectPostingCurrencyDifferentFromTheAccount()
    {
        var (account, category) = await AddOrdinaryContextAsync();
        var income = await AddIncomeAsync(account, category);
        ClearTracker();

        Context.AccountPostings.Add(new AccountPostingRecord
        {
            FinancialOperationId = income.Id,
            AccountId = account.Id,
            Kind = PostingKind.Income,
            CurrencyCode = "USD",
            SignedAmount = 10m,
        });

        await Should.ThrowAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }

    private async Task<(Account Account, Category Category)> AddOrdinaryContextAsync()
    {
        var account = new Account(
            Guid.NewGuid(), "Conto", AccountType.Checking, Currency.Parse("EUR"), AccountStatus.Active);
        var category = new Category(Guid.NewGuid(), "Categoria", CategoryType.Income);
        await new AccountRepository(Context).AddAsync(account);
        await new CategoryRepository(Context).AddAsync(category);
        return (account, category);
    }

    private Task<Income> AddIncomeAsync(Account account, Category category) =>
        AddIncomeAsync(account, category, 10m);

    private async Task<Income> AddIncomeAsync(Account account, Category category, decimal amount)
    {
        var income = new Income(
            Guid.NewGuid(),
            account,
            category,
            new PositiveMoney(new Money(amount, Currency.Parse("EUR"))),
            new DateOnly(2026, 3, 24),
            new DateTimeOffset(2026, 3, 24, 8, 0, 0, TimeSpan.Zero));
        await new FinancialOperationRepository(Context).AddAsync(income);
        return income;
    }
}
