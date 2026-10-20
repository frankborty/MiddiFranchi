using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Configurations;

internal sealed class InvestmentPortfolioRecordConfiguration : IEntityTypeConfiguration<InvestmentPortfolioRecord>
{
    public void Configure(EntityTypeBuilder<InvestmentPortfolioRecord> builder)
    {
        builder.ToTable("InvestmentPortfolios", table =>
        {
            table.HasCheckConstraint(
                "CK_InvestmentPortfolios_ReferenceCurrencyCode",
                $"\"ReferenceCurrencyCode\" IN ({PersistenceConfigurationHelpers.SupportedCurrenciesSql})");
        });

        builder.HasKey(portfolio => portfolio.Id);
        builder.Property(portfolio => portfolio.Id).ValueGeneratedNever();
        builder.Property(portfolio => portfolio.Name).IsRequiredText();
        builder.Property(portfolio => portfolio.ReferenceCurrencyCode).IsCurrencyCode();
    }
}

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
        builder.Property(instrument => instrument.Type).IsEnumString();
        builder.Property(instrument => instrument.QuoteCurrencyCode).IsCurrencyCode();
        builder.Property(instrument => instrument.PriceUnit).IsEnumString();
        builder.Property(instrument => instrument.NotionalValueAmount).IsOptionalNumeric();
        builder.Property(instrument => instrument.NotionalValueCurrencyCode).IsOptionalCurrencyCode();
    }
}
