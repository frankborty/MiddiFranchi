using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Configurations;

internal sealed class AccountRecordConfiguration : IEntityTypeConfiguration<AccountRecord>
{
    public void Configure(EntityTypeBuilder<AccountRecord> builder)
    {
        builder.ToTable("Accounts", table =>
        {
            table.HasCheckConstraint(
                "CK_Accounts_CurrencyCode",
                $"\"CurrencyCode\" IN ({PersistenceConfigurationHelpers.SupportedCurrenciesSql})");
            table.HasCheckConstraint(
                "CK_Accounts_Type",
                "\"Type\" IN ('Checking', 'Card', 'Cash', 'Liability', 'Loans')");
            table.HasCheckConstraint(
                "CK_Accounts_Status",
                "\"Status\" IN ('Active', 'Closed')");
        });

        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id).ValueGeneratedNever();
        builder.HasAlternateKey(account => new { account.Id, account.CurrencyCode });
        builder.Property(account => account.Name).IsRequiredText();
        builder.Property(account => account.Type).IsEnumString();
        builder.Property(account => account.CurrencyCode).IsCurrencyCode();
        builder.Property(account => account.Status).IsEnumString();
        builder.Property(account => account.ColorHex).HasMaxLength(7);
        builder.Property(account => account.IconDataUrl);
    }
}

internal sealed class CategoryRecordConfiguration : IEntityTypeConfiguration<CategoryRecord>
{
    public void Configure(EntityTypeBuilder<CategoryRecord> builder)
    {
        builder.ToTable("Categories", table =>
        {
            table.HasCheckConstraint(
                "CK_Categories_Type",
                "\"Type\" IN ('Income', 'Expense')");
        });

        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedNever();
        builder.Property(category => category.Name).IsRequiredText();
        builder.Property(category => category.Type).IsEnumString();
        builder.Property(category => category.Description).IsOptionalText();
    }
}
