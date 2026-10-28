using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Configurations;

internal sealed class PriceObservationRecordConfiguration : IEntityTypeConfiguration<PriceObservationRecord>
{
    public void Configure(EntityTypeBuilder<PriceObservationRecord> builder)
    {
        builder.ToTable("PriceObservations", table =>
        {
            table.HasCheckConstraint(
                "CK_PriceObservations_Price",
                "\"Price\" > 0");
            table.HasCheckConstraint(
                "CK_PriceObservations_Unit",
                "\"Unit\" IN ('PerUnit', 'PercentOfNominal')");
            table.HasCheckConstraint(
                "CK_PriceObservations_PriceCurrency",
                "(\"Unit\" = 'PerUnit' AND \"PriceCurrencyCode\" IS NOT NULL AND " +
                $"\"PriceCurrencyCode\" IN ({PersistenceConfigurationHelpers.SupportedCurrenciesSql})) OR " +
                "(\"Unit\" = 'PercentOfNominal' AND \"PriceCurrencyCode\" IS NULL)");
        });

        builder.HasKey(observation => observation.Id);
        builder.Property(observation => observation.Id).ValueGeneratedNever();
        builder.Property(observation => observation.Unit).IsEnumString();
        builder.Property(observation => observation.Price).IsNumeric();
        builder.Property(observation => observation.PriceCurrencyCode).IsOptionalCurrencyCode();
        builder.Property(observation => observation.ValidAt).HasColumnType("date").IsRequired();
        builder.Property(observation => observation.ObservedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(observation => observation.TradeTime).HasColumnType("time without time zone");
        builder.Property(observation => observation.Source).IsRequiredText();

        builder.HasOne(observation => observation.Instrument)
            .WithMany()
            .HasForeignKey(observation => observation.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(observation => new { observation.InstrumentId, observation.ValidAt, observation.ObservedAt });
    }
}

