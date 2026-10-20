using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MiddiFranchi.Infrastructure.Persistence;

namespace MiddiFranchi.Web;

public sealed class MiddiFranchiDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MiddiFranchiDbContext>
{
    public MiddiFranchiDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Per i comandi EF impostare la variabile d'ambiente " +
                "'ConnectionStrings__DefaultConnection'. Non usare credenziali versionate.");
        }

        var options = new DbContextOptionsBuilder<MiddiFranchiDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(MiddiFranchiDbContext).Assembly.GetName().Name))
            .Options;

        return new MiddiFranchiDbContext(options);
    }
}
