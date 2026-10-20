using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace MiddiFranchi.IntegrationTests.Infrastructure;

public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public bool Enabled => PostgresTestSettings.Enabled;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (!Enabled)
        {
            return;
        }

        _container = new PostgreSqlBuilder("postgres:17")
            .WithDatabase("middifranchi_tests")
            .WithUsername("postgres")
            .WithPassword(Guid.NewGuid().ToString("N"))
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public MiddiFranchiDbContext CreateContext()
    {
        if (!Enabled)
        {
            throw new InvalidOperationException(
                $"Il fixture PostgreSQL è disabilitato. Impostare {PostgresTestSettings.EnableVariable}=1.");
        }

        var options = new DbContextOptionsBuilder<MiddiFranchiDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(MiddiFranchiDbContext).Assembly.GetName().Name))
            .Options;

        return new MiddiFranchiDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(PostgresCollection.Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresDatabaseFixture>
{
    public const string Name = "postgres";
}
