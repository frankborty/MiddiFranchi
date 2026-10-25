using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiddiFranchi.Application.DataManagement;
using MiddiFranchi.Application.Financial;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Infrastructure.DataManagement;
using MiddiFranchi.Infrastructure.Persistence.Repositories;
using MiddiFranchi.Infrastructure.PriceUpdates;

namespace MiddiFranchi.Infrastructure.Persistence;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMiddiFranchiInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<MiddiFranchiDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "La connection string 'ConnectionStrings:DefaultConnection' non è configurata. " +
                    "Usare user secrets o variabili d'ambiente; non inserire credenziali nei file versionati.");
            }

            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(MiddiFranchiDbContext).Assembly.GetName().Name));
        });

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IInstrumentRepository, InstrumentRepository>();
        services.AddScoped<IFinancialOperationRepository, FinancialOperationRepository>();
        services.AddScoped<IAccountPostingRepository, AccountPostingRepository>();
        services.AddScoped<IPriceObservationRepository, PriceObservationRepository>();
        services.AddScoped<IManualBalanceRepository, ManualBalanceRepository>();
        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<ILoanReadingRepository, LoanReadingRepository>();
        services.AddScoped<IAppMetadataRepository, AppMetadataRepository>();
        services.AddScoped<IDataSnapshotRepository, SqlDataSnapshotRepository>();
        services.AddScoped<ITransactionRunner, EfTransactionRunner>();

        services.AddSingleton<IInvestmentQuoteProvider, BorsaItalianaQuoteProvider>();
        services.AddSingleton<JsonPriceUpdateStore>();
        services.AddSingleton<IPriceUpdateStore>(serviceProvider =>
            serviceProvider.GetRequiredService<JsonPriceUpdateStore>());
        services.AddSingleton<IPriceUpdateBackupParticipant>(serviceProvider =>
            serviceProvider.GetRequiredService<JsonPriceUpdateStore>());

        return services;
    }
}
