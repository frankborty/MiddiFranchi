using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Configurations;

internal sealed class LoanRecordConfiguration : IEntityTypeConfiguration<LoanRecord>
{
    public void Configure(EntityTypeBuilder<LoanRecord> builder)
    {
        builder.ToTable("Loans");

        builder.HasKey(loan => loan.Id);
        builder.Property(loan => loan.Id).ValueGeneratedNever();
        builder.Property(loan => loan.Borrower).IsRequiredText();
        builder.Property(loan => loan.CreatedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(loan => loan.Account)
            .WithMany()
            .HasForeignKey(loan => loan.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(loan => loan.AccountId);
    }
}

internal sealed class LoanReadingRecordConfiguration : IEntityTypeConfiguration<LoanReadingRecord>
{
    public void Configure(EntityTypeBuilder<LoanReadingRecord> builder)
    {
        builder.ToTable("LoanReadings", table =>
        {
            table.HasCheckConstraint(
                "CK_LoanReadings_CurrencyCode",
                $"\"AmountCurrencyCode\" IN ({PersistenceConfigurationHelpers.SupportedCurrenciesSql})");
        });

        builder.HasKey(reading => reading.Id);
        builder.Property(reading => reading.Id).ValueGeneratedNever();
        builder.Property(reading => reading.AmountAmount).IsNumeric();
        builder.Property(reading => reading.AmountCurrencyCode).IsCurrencyCode();
        builder.Property(reading => reading.RecordedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasOne(reading => reading.Loan)
            .WithMany(loan => loan.Readings)
            .HasForeignKey(reading => reading.LoanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(reading => new { reading.LoanId, reading.ValidAt });
        builder.HasIndex(reading => new { reading.LoanId, reading.RecordedAt });
    }
}
