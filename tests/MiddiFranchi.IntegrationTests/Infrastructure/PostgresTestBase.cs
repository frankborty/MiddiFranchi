using Microsoft.EntityFrameworkCore.Storage;
using MiddiFranchi.Infrastructure.Persistence;
using Xunit;

namespace MiddiFranchi.IntegrationTests.Infrastructure;

public abstract class PostgresTestBase : IAsyncLifetime
{
    private IDbContextTransaction? _transaction;

    protected PostgresTestBase(PostgresDatabaseFixture fixture)
    {
        Fixture = fixture;
    }

    protected PostgresDatabaseFixture Fixture { get; }

    protected MiddiFranchiDbContext Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (!Fixture.Enabled)
        {
            return;
        }

        Context = Fixture.CreateContext();
        _transaction = await Context.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
        }

        if (Context is not null)
        {
            await Context.DisposeAsync();
        }
    }

    protected void ClearTracker() => Context.ChangeTracker.Clear();
}
