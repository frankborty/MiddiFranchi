using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MiddiFranchi.Infrastructure.Persistence.Configurations;

internal static class PersistenceConfigurationHelpers
{
    public const string SupportedCurrenciesSql = "'EUR'";

    public static PropertyBuilder<string> IsRequiredText(this PropertyBuilder<string> property) =>
        property.HasColumnType("text").IsRequired();

    public static PropertyBuilder<string?> IsOptionalText(this PropertyBuilder<string?> property) =>
        property.HasColumnType("text");

    public static PropertyBuilder<string> IsCurrencyCode(this PropertyBuilder<string> property) =>
        property.HasMaxLength(3).IsRequired();

    public static PropertyBuilder<string?> IsOptionalCurrencyCode(this PropertyBuilder<string?> property) =>
        property.HasMaxLength(3);

    public static PropertyBuilder<decimal> IsNumeric(this PropertyBuilder<decimal> property) =>
        property.HasColumnType("numeric");

    public static PropertyBuilder<decimal?> IsOptionalNumeric(this PropertyBuilder<decimal?> property) =>
        property.HasColumnType("numeric");

    public static PropertyBuilder<TEnum> IsEnumString<TEnum>(this PropertyBuilder<TEnum> property)
        where TEnum : struct, Enum =>
        property.HasConversion<string>().HasMaxLength(32).IsRequired();

    public static PropertyBuilder<TEnum?> IsOptionalEnumString<TEnum>(this PropertyBuilder<TEnum?> property)
        where TEnum : struct, Enum =>
        property.HasConversion<string>().HasMaxLength(32);
}
