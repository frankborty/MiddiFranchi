using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Configurations;

internal sealed class ManualBalanceRecordConfiguration : IEntityTypeConfiguration<ManualBalanceRecord>
{
    public void Configure(EntityTypeBuilder<ManualBalanceRecord> builder)
    {
        builder.ToTable("ManualBalances", table =>
        {
            table.HasCheckConstraint(
                "CK_ManualBalances_CurrencyCode",
                $"\"AmountCurrencyCode\" IN ({PersistenceConfigurationHelpers.SupportedCurrenciesSql})");
        });

        builder.HasKey(reading => reading.Id);
        builder.Property(reading => reading.Id).ValueGeneratedNever();
        builder.Property(reading => reading.AmountAmount).IsNumeric();
        builder.Property(reading => reading.AmountCurrencyCode).IsCurrencyCode();
        builder.Property(reading => reading.RecordedAt).HasColumnType("timestamptz");

        builder.HasOne(reading => reading.Account)
            .WithMany()
            .HasForeignKey(reading => new { reading.AccountId, reading.AmountCurrencyCode })
            .HasPrincipalKey(account => new { account.Id, account.CurrencyCode })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(reading => new { reading.AccountId, reading.ValidAt });
        builder.HasIndex(reading => new { reading.AccountId, reading.RecordedAt });
    }
}
