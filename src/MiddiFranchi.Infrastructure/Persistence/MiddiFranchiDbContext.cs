using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence;

public sealed class MiddiFranchiDbContext : DbContext
{
    public MiddiFranchiDbContext(DbContextOptions<MiddiFranchiDbContext> options)
        : base(options)
    {
    }

    public DbSet<AccountRecord> Accounts => Set<AccountRecord>();

    public DbSet<CategoryRecord> Categories => Set<CategoryRecord>();


    public DbSet<InstrumentRecord> Instruments => Set<InstrumentRecord>();

    public DbSet<FinancialOperationRecord> FinancialOperations => Set<FinancialOperationRecord>();

    public DbSet<AccountPostingRecord> AccountPostings => Set<AccountPostingRecord>();

    public DbSet<PriceObservationRecord> PriceObservations => Set<PriceObservationRecord>();

    public DbSet<ManualBalanceRecord> ManualBalances => Set<ManualBalanceRecord>();

    public DbSet<LoanRecord> Loans => Set<LoanRecord>();

    public DbSet<LoanReadingRecord> LoanReadings => Set<LoanReadingRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MiddiFranchiDbContext).Assembly);
    }
}
