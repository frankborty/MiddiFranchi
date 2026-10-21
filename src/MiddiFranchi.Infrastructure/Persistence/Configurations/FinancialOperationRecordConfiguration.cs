using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Configurations;

internal sealed class FinancialOperationRecordConfiguration : IEntityTypeConfiguration<FinancialOperationRecord>
{
    public void Configure(EntityTypeBuilder<FinancialOperationRecord> builder)
    {
        builder.ToTable("FinancialOperations", table =>
        {
            table.HasCheckConstraint(
                "CK_FinancialOperations_Kind",
                "\"Kind\" IN ('Income', 'Expense', 'Transfer', 'Investment', 'Reversal')");
            table.HasCheckConstraint(
                "CK_FinancialOperations_InvestmentKind",
                "\"InvestmentKind\" IS NULL OR \"InvestmentKind\" IN ('Buy', 'Sell', 'Dividend', 'Coupon', 'StandaloneFee')");
            table.HasCheckConstraint(
                "CK_FinancialOperations_QuotedPriceUnit",
                "\"QuotedPriceUnit\" IS NULL OR \"QuotedPriceUnit\" IN ('PerUnit', 'PercentOfNominal')");
            table.HasCheckConstraint(
                "CK_FinancialOperations_AmountCurrency",
                "\"AmountCurrencyCode\" IS NULL OR " +
                $"\"AmountCurrencyCode\" IN ({PersistenceConfigurationHelpers.SupportedCurrenciesSql})");
            table.HasCheckConstraint(
                "CK_FinancialOperations_SourceAmountCurrency",
                "\"SourceAmountCurrencyCode\" IS NULL OR " +
                $"\"SourceAmountCurrencyCode\" IN ({PersistenceConfigurationHelpers.SupportedCurrenciesSql})");
            table.HasCheckConstraint(
                "CK_FinancialOperations_DestinationAmountCurrency",
                "\"DestinationAmountCurrencyCode\" IS NULL OR " +
                $"\"DestinationAmountCurrencyCode\" IN ({PersistenceConfigurationHelpers.SupportedCurrenciesSql})");
            table.HasCheckConstraint(
                "CK_FinancialOperations_QuotedPriceCurrency",
                "\"QuotedPricePriceCurrencyCode\" IS NULL OR " +
                $"\"QuotedPricePriceCurrencyCode\" IN ({PersistenceConfigurationHelpers.SupportedCurrenciesSql})");
            table.HasCheckConstraint(
                "CK_FinancialOperations_AmountPair",
                "(\"AmountAmount\" IS NULL AND \"AmountCurrencyCode\" IS NULL) OR " +
                "(\"AmountAmount\" IS NOT NULL AND \"AmountAmount\" > 0 AND \"AmountCurrencyCode\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_FinancialOperations_SourceAmountPair",
                "(\"SourceAmountAmount\" IS NULL AND \"SourceAmountCurrencyCode\" IS NULL) OR " +
                "(\"SourceAmountAmount\" IS NOT NULL AND \"SourceAmountAmount\" > 0 AND \"SourceAmountCurrencyCode\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_FinancialOperations_DestinationAmountPair",
                "(\"DestinationAmountAmount\" IS NULL AND \"DestinationAmountCurrencyCode\" IS NULL) OR " +
                "(\"DestinationAmountAmount\" IS NOT NULL AND \"DestinationAmountAmount\" > 0 AND \"DestinationAmountCurrencyCode\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_FinancialOperations_Quantity",
                "\"QuantityValue\" IS NULL OR \"QuantityValue\" > 0");
            table.HasCheckConstraint(
                "CK_FinancialOperations_QuotedPriceShape",
                "(\"QuotedPriceUnit\" IS NULL AND \"QuotedPricePriceAmount\" IS NULL AND " +
                "\"QuotedPricePriceCurrencyCode\" IS NULL AND \"QuotedPricePercentOfNominal\" IS NULL) OR " +
                "(\"QuotedPriceUnit\" = 'PerUnit' AND \"QuotedPricePriceAmount\" IS NOT NULL AND " +
                "\"QuotedPricePriceAmount\" > 0 AND \"QuotedPricePriceCurrencyCode\" IS NOT NULL AND " +
                "\"QuotedPricePercentOfNominal\" IS NULL) OR " +
                "(\"QuotedPriceUnit\" = 'PercentOfNominal' AND \"QuotedPricePercentOfNominal\" IS NOT NULL AND " +
                "\"QuotedPricePercentOfNominal\" > 0 AND \"QuotedPricePriceAmount\" IS NULL AND " +
                "\"QuotedPricePriceCurrencyCode\" IS NULL)");
            table.HasCheckConstraint(
                "CK_FinancialOperations_ReversalLink",
                "(\"Kind\" = 'Reversal' AND \"OriginalOperationId\" IS NOT NULL) OR " +
                "(\"Kind\" <> 'Reversal' AND \"OriginalOperationId\" IS NULL)");
            table.HasCheckConstraint(
                "CK_FinancialOperations_InvestmentFields",
                "\"Kind\" <> 'Investment' OR (\"InvestmentKind\" IS NOT NULL AND " +
                "\"PortfolioId\" IS NOT NULL AND \"SettlementAccountId\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_FinancialOperations_OrdinaryFields",
                "\"Kind\" NOT IN ('Income', 'Expense') OR (\"AccountId\" IS NOT NULL AND " +
                "\"CategoryId\" IS NOT NULL AND \"AmountAmount\" IS NOT NULL AND \"AmountCurrencyCode\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_FinancialOperations_TransferFields",
                "\"Kind\" <> 'Transfer' OR (\"SourceAccountId\" IS NOT NULL AND " +
                "\"DestinationAccountId\" IS NOT NULL AND \"SourceAmountAmount\" IS NOT NULL AND " +
                "\"SourceAmountCurrencyCode\" IS NOT NULL AND \"DestinationAmountAmount\" IS NOT NULL AND " +
                "\"DestinationAmountCurrencyCode\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_FinancialOperations_NonInvestmentFields",
                "\"Kind\" = 'Investment' OR (\"InvestmentKind\" IS NULL AND " +
                "\"PortfolioId\" IS NULL AND \"InstrumentId\" IS NULL AND " +
                "\"SettlementAccountId\" IS NULL AND \"QuantityValue\" IS NULL AND " +
                "\"QuotedPriceUnit\" IS NULL AND \"QuotedPricePriceAmount\" IS NULL AND " +
                "\"QuotedPricePriceCurrencyCode\" IS NULL AND \"QuotedPricePercentOfNominal\" IS NULL)");
            table.HasCheckConstraint(
                "CK_FinancialOperations_NonTransferFields",
                "\"Kind\" = 'Transfer' OR (\"SourceAccountId\" IS NULL AND " +
                "\"DestinationAccountId\" IS NULL AND \"SourceAmountAmount\" IS NULL AND " +
                "\"SourceAmountCurrencyCode\" IS NULL AND \"DestinationAmountAmount\" IS NULL AND " +
                "\"DestinationAmountCurrencyCode\" IS NULL)");
            table.HasCheckConstraint(
                "CK_FinancialOperations_NonOrdinaryFields",
                "\"Kind\" IN ('Income', 'Expense') OR (\"AccountId\" IS NULL AND \"CategoryId\" IS NULL)");
            table.HasCheckConstraint(
                "CK_FinancialOperations_AmountByKind",
                "(\"Kind\" IN ('Income', 'Expense') AND \"AmountAmount\" IS NOT NULL AND \"AmountCurrencyCode\" IS NOT NULL) OR " +
                "(\"Kind\" = 'Investment' AND \"InvestmentKind\" IN ('Dividend', 'Coupon') AND " +
                "\"AmountAmount\" IS NOT NULL AND \"AmountCurrencyCode\" IS NOT NULL) OR " +
                "(\"Kind\" IN ('Transfer', 'Reversal') AND \"AmountAmount\" IS NULL AND \"AmountCurrencyCode\" IS NULL) OR " +
                "(\"Kind\" = 'Investment' AND \"InvestmentKind\" IN ('Buy', 'Sell', 'StandaloneFee') AND " +
                "\"AmountAmount\" IS NULL AND \"AmountCurrencyCode\" IS NULL)");
            table.HasCheckConstraint(
                "CK_FinancialOperations_InvestmentDetail",
                "\"Kind\" <> 'Investment' OR " +
                "(\"InvestmentKind\" IN ('Buy', 'Sell') AND \"InstrumentId\" IS NOT NULL AND " +
                "\"QuantityValue\" IS NOT NULL AND \"QuotedPriceUnit\" IS NOT NULL AND " +
                "\"AmountAmount\" IS NULL AND \"AmountCurrencyCode\" IS NULL) OR " +
                "(\"InvestmentKind\" IN ('Dividend', 'Coupon') AND \"InstrumentId\" IS NOT NULL AND " +
                "\"QuantityValue\" IS NULL AND \"QuotedPriceUnit\" IS NULL AND " +
                "\"QuotedPricePriceAmount\" IS NULL AND \"QuotedPricePriceCurrencyCode\" IS NULL AND " +
                "\"QuotedPricePercentOfNominal\" IS NULL AND \"AmountAmount\" IS NOT NULL AND " +
                "\"AmountCurrencyCode\" IS NOT NULL) OR " +
                "(\"InvestmentKind\" = 'StandaloneFee' AND \"QuantityValue\" IS NULL AND " +
                "\"QuotedPriceUnit\" IS NULL AND \"QuotedPricePriceAmount\" IS NULL AND " +
                "\"QuotedPricePriceCurrencyCode\" IS NULL AND \"QuotedPricePercentOfNominal\" IS NULL AND " +
                "\"AmountAmount\" IS NULL AND \"AmountCurrencyCode\" IS NULL)");
            table.HasCheckConstraint(
                "CK_FinancialOperations_DistinctTransferAccounts",
                "\"Kind\" <> 'Transfer' OR \"SourceAccountId\" <> \"DestinationAccountId\"");
            table.HasCheckConstraint(
                "CK_FinancialOperations_TransferEurAmounts",
                "\"Kind\" <> 'Transfer' OR (\"SourceAmountCurrencyCode\" = 'EUR' AND " +
                "\"DestinationAmountCurrencyCode\" = 'EUR' AND " +
                "\"SourceAmountAmount\" = \"DestinationAmountAmount\")");
        });

        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.Id).ValueGeneratedNever();
        builder.Property(operation => operation.Kind).IsEnumString();
        builder.Property(operation => operation.OperationDate).HasColumnType("date").IsRequired();
        builder.Property(operation => operation.RecordedAt).HasColumnType("timestamptz").IsRequired();
        builder.Property(operation => operation.Description).IsOptionalText();
        builder.Property(operation => operation.Notes).IsOptionalText();
        builder.Property(operation => operation.OriginalOperationId);
        builder.Property(operation => operation.AmountAmount).IsOptionalNumeric();
        builder.Property(operation => operation.AmountCurrencyCode).IsOptionalCurrencyCode();
        builder.Property(operation => operation.SourceAmountAmount).IsOptionalNumeric();
        builder.Property(operation => operation.SourceAmountCurrencyCode).IsOptionalCurrencyCode();
        builder.Property(operation => operation.DestinationAmountAmount).IsOptionalNumeric();
        builder.Property(operation => operation.DestinationAmountCurrencyCode).IsOptionalCurrencyCode();
        builder.Property(operation => operation.InvestmentKind).IsOptionalEnumString();
        builder.Property(operation => operation.QuantityValue).IsOptionalNumeric();
        builder.Property(operation => operation.QuotedPriceUnit).IsOptionalEnumString();
        builder.Property(operation => operation.QuotedPricePriceAmount).IsOptionalNumeric();
        builder.Property(operation => operation.QuotedPricePriceCurrencyCode).IsOptionalCurrencyCode();
        builder.Property(operation => operation.QuotedPricePercentOfNominal).IsOptionalNumeric();

        builder.HasOne(operation => operation.Account)
            .WithMany()
            .HasForeignKey(operation => operation.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(operation => operation.Category)
            .WithMany()
            .HasForeignKey(operation => operation.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(operation => operation.SourceAccount)
            .WithMany()
            .HasForeignKey(operation => operation.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(operation => operation.DestinationAccount)
            .WithMany()
            .HasForeignKey(operation => operation.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(operation => operation.Portfolio)
            .WithMany()
            .HasForeignKey(operation => operation.PortfolioId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(operation => operation.Instrument)
            .WithMany()
            .HasForeignKey(operation => operation.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(operation => operation.SettlementAccount)
            .WithMany()
            .HasForeignKey(operation => operation.SettlementAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancialOperationRecord>()
            .WithMany()
            .HasForeignKey(operation => operation.OriginalOperationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(operation => operation.OperationDate);
        builder.HasIndex(operation => operation.Kind);
        builder.HasIndex(operation => operation.AccountId);
        builder.HasIndex(operation => operation.PortfolioId);
        builder.HasIndex(operation => operation.InstrumentId);
        builder.HasIndex(operation => operation.OriginalOperationId).IsUnique();
    }
}
