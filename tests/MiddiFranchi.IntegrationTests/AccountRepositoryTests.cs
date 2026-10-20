using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.Persistence.Repositories;
using MiddiFranchi.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class AccountRepositoryTests : PostgresTestBase
{
    public AccountRepositoryTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task AddAndGet_ShouldRoundTripAccount()
    {
        var account = new Account(
            Guid.NewGuid(),
            "Conto principale",
            AccountType.Checking,
            Currency.Parse("USD"),
            AccountStatus.Active);
        var repository = new AccountRepository(Context);

        await repository.AddAsync(account);
        ClearTracker();

        var loaded = await repository.GetByIdAsync(account.Id);

        loaded.ShouldNotBeNull();
        loaded.Id.ShouldBe(account.Id);
        loaded.Name.ShouldBe(account.Name);
        loaded.Type.ShouldBe(account.Type);
        loaded.Currency.ShouldBe(account.Currency);
        loaded.Status.ShouldBe(account.Status);
    }

    [PostgresFact]
    public async Task List_ShouldReturnAccountsInDeterministicOrder()
    {
        var repository = new AccountRepository(Context);
        var first = new Account(Guid.NewGuid(), "Alfa", AccountType.Cash, Currency.Parse("EUR"), AccountStatus.Active);
        var second = new Account(Guid.NewGuid(), "Beta", AccountType.Checking, Currency.Parse("EUR"), AccountStatus.Active);

        await repository.AddAsync(second);
        await repository.AddAsync(first);
        ClearTracker();

        var accounts = await repository.ListAsync();

        accounts.Select(account => account.Id).ShouldBe([first.Id, second.Id]);
    }

    [PostgresFact]
    public async Task GetById_ShouldReturnNullWhenMissing()
    {
        var repository = new AccountRepository(Context);

        (await repository.GetByIdAsync(Guid.NewGuid())).ShouldBeNull();
    }

    [PostgresFact]
    public async Task AddDuplicateId_ShouldBeRejectedByTheDatabase()
    {
        var repository = new AccountRepository(Context);
        var account = new Account(Guid.NewGuid(), "Conto", AccountType.Checking, Currency.Parse("EUR"), AccountStatus.Active);

        await repository.AddAsync(account);
        ClearTracker();

        var duplicate = new Account(account.Id, "Duplicato", AccountType.Checking, Currency.Parse("EUR"), AccountStatus.Active);

        await Should.ThrowAsync<DbUpdateException>(() => repository.AddAsync(duplicate));
    }
}
