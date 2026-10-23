using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Configurations;

internal sealed class InstrumentRecordConfiguration : IEntityTypeConfiguration<InstrumentRecord>
{
    public void Configure(EntityTypeBuilder<InstrumentRecord> builder)
    {
        builder.ToTable("Instruments", table =>
        {
            table.HasCheckConstraint(
                "CK_Instruments_QuoteCurrencyCode",
                $"\"QuoteCurrencyCode\" IN ({PersistenceConfigurationHelpers.SupportedCurrenciesSql})");
            table.HasCheckConstraint(
                "CK_Instruments_PriceUnit",
                "\"PriceUnit\" IN ('PerUnit', 'PercentOfNominal')");
            table.HasCheckConstraint(
                "CK_Instruments_AssetClass",
                "\"AssetClass\" IN ('Equity', 'Bond', 'MoneyMarket', 'RealEstate', 'Commodity', " +
                "'Crypto', 'Mixed', 'Pension', 'Other')");
            table.HasCheckConstraint(
                "CK_Instruments_Status",
                "\"Status\" IN ('Active', 'Archived')");
            table.HasCheckConstraint(
                "CK_Instruments_IsinFormat",
                "\"Isin\" IS NULL OR \"Isin\" ~ '^[A-Z0-9]{12}$'");
            table.HasCheckConstraint(
                "CK_Instruments_CouponRate",
                "\"CouponRate\" IS NULL OR (\"CouponRate\" > 0 AND \"CouponRate\" <= 100)");
            table.HasCheckConstraint(
                "CK_Instruments_BondDetails",
                "\"AssetClass\" = 'Bond' OR (\"MaturityDate\" IS NULL AND \"CouponRate\" IS NULL)");
            table.HasCheckConstraint(
                "CK_Instruments_NotionalValue",
                "(\"NotionalValueAmount\" IS NULL AND \"NotionalValueCurrencyCode\" IS NULL) OR " +
                "(\"NotionalValueAmount\" IS NOT NULL AND \"NotionalValueAmount\" > 0 AND " +
                $"\"NotionalValueCurrencyCode\" IN ({PersistenceConfigurationHelpers.SupportedCurrenciesSql}))");
            table.HasCheckConstraint(
                "CK_Instruments_NotionalValueCurrencyMatchesQuote",
                "\"NotionalValueCurrencyCode\" IS NULL OR \"NotionalValueCurrencyCode\" = \"QuoteCurrencyCode\"");
            table.HasCheckConstraint(
                "CK_Instruments_PriceUnitNotional",
                "(\"PriceUnit\" = 'PercentOfNominal' AND \"NotionalValueAmount\" IS NOT NULL AND " +
                "\"NotionalValueAmount\" > 0 AND \"NotionalValueCurrencyCode\" IS NOT NULL) OR " +
                "(\"PriceUnit\" = 'PerUnit' AND \"NotionalValueAmount\" IS NULL AND " +
                "\"NotionalValueCurrencyCode\" IS NULL)");
        });

        builder.HasKey(instrument => instrument.Id);
        builder.Property(instrument => instrument.Id).ValueGeneratedNever();
        builder.Property(instrument => instrument.Name).IsRequiredText();
        builder.Property(instrument => instrument.Symbol).HasMaxLength(32);
        builder.Property(instrument => instrument.Isin).HasMaxLength(12);
        builder.Property(instrument => instrument.AssetClass).IsEnumString();
        builder.Property(instrument => instrument.Status).IsEnumString();
        builder.Property(instrument => instrument.QuoteCurrencyCode).IsCurrencyCode();
        builder.Property(instrument => instrument.PriceUnit).IsEnumString();
        builder.Property(instrument => instrument.NotionalValueAmount).IsOptionalNumeric();
        builder.Property(instrument => instrument.NotionalValueCurrencyCode).IsOptionalCurrencyCode();
        builder.Property(instrument => instrument.ReferenceUrl).HasMaxLength(500);
        builder.Property(instrument => instrument.Notes).IsOptionalText();
        builder.Property(instrument => instrument.MaturityDate).HasColumnType("date");
        builder.Property(instrument => instrument.CouponRate).IsOptionalNumeric();

        builder.HasIndex(instrument => instrument.Isin)
            .IsUnique()
            .HasFilter("\"Isin\" IS NOT NULL");
    }
}
