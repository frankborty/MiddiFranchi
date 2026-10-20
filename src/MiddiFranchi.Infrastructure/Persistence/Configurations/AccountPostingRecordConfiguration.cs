using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Configurations;

internal sealed class AccountPostingRecordConfiguration : IEntityTypeConfiguration<AccountPostingRecord>
{
    public void Configure(EntityTypeBuilder<AccountPostingRecord> builder)
    {
        builder.ToTable("AccountPostings", table =>
        {
            table.HasCheckConstraint(
                "CK_AccountPostings_SignedAmount",
                "\"SignedAmount\" <> 0");
            table.HasCheckConstraint(
                "CK_AccountPostings_SignByKind",
                "(\"Kind\" <> 'Income' OR \"SignedAmount\" > 0) AND " +
                "(\"Kind\" <> 'Fee' OR \"SignedAmount\" < 0)");
            table.HasCheckConstraint(
                "CK_AccountPostings_Kind",
                "\"Kind\" IN ('Capital', 'Fee', 'Income', 'TransferLeg')");
            table.HasCheckConstraint(
                "CK_AccountPostings_CurrencyCode",
                $"\"CurrencyCode\" IN ({PersistenceConfigurationHelpers.SupportedCurrenciesSql})");
        });

        builder.HasKey(posting => posting.Id);
        builder.Property(posting => posting.Id).ValueGeneratedOnAdd();
        builder.Property(posting => posting.Kind).IsEnumString();
        builder.Property(posting => posting.CurrencyCode).IsCurrencyCode();
        builder.Property(posting => posting.SignedAmount).IsNumeric();

        builder.HasOne(posting => posting.Operation)
            .WithMany(operation => operation.Postings)
            .HasForeignKey(posting => posting.FinancialOperationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(posting => posting.Account)
            .WithMany()
            .HasForeignKey(posting => new { posting.AccountId, posting.CurrencyCode })
            .HasPrincipalKey(account => new { account.Id, account.CurrencyCode })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(posting => posting.AccountId);
        builder.HasIndex(posting => posting.FinancialOperationId);
        builder.HasIndex(posting => new { posting.AccountId, posting.FinancialOperationId });
    }
}

internal sealed class FeeComponentRecordConfiguration : IEntityTypeConfiguration<FeeComponentRecord>
{
    public void Configure(EntityTypeBuilder<FeeComponentRecord> builder)
    {
        builder.ToTable("InvestmentOperationFees", table =>
        {
            table.HasCheckConstraint(
                "CK_InvestmentOperationFees_Amount",
                "\"AmountAmount\" > 0");
            table.HasCheckConstraint(
                "CK_InvestmentOperationFees_CurrencyCode",
                $"\"AmountCurrencyCode\" IN ({PersistenceConfigurationHelpers.SupportedCurrenciesSql})");
            table.HasCheckConstraint(
                "CK_InvestmentOperationFees_Ordinal",
                "\"Ordinal\" >= 0");
            table.HasCheckConstraint(
                "CK_InvestmentOperationFees_Kind",
                "\"Kind\" IN ('BuyFee', 'SellFee', 'Standalone', 'TaxWithheld', 'Other')");
        });

        builder.HasKey(fee => fee.Id);
        builder.Property(fee => fee.Id).ValueGeneratedOnAdd();
        builder.Property(fee => fee.Ordinal).IsRequired();
        builder.Property(fee => fee.Kind).IsEnumString();
        builder.Property(fee => fee.AmountAmount).IsNumeric();
        builder.Property(fee => fee.AmountCurrencyCode).IsCurrencyCode();

        builder.HasOne(fee => fee.InvestmentOperation)
            .WithMany(operation => operation.Fees)
            .HasForeignKey(fee => fee.InvestmentOperationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(fee => fee.DebitAccount)
            .WithMany()
            .HasForeignKey(fee => fee.DebitAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(fee => fee.InvestmentOperationId);
        builder.HasIndex(fee => new { fee.InvestmentOperationId, fee.Ordinal }).IsUnique();
    }
}
