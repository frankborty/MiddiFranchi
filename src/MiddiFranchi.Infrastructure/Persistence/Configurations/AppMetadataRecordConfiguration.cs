using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Configurations;

internal sealed class AppMetadataRecordConfiguration : IEntityTypeConfiguration<AppMetadataRecord>
{
    public void Configure(EntityTypeBuilder<AppMetadataRecord> builder)
    {
        builder.ToTable("AppMetadata");

        builder.HasKey(metadata => metadata.Key);
        builder.Property(metadata => metadata.Key).IsRequiredText();
        builder.Property(metadata => metadata.Value).IsRequiredText();
        builder.Property(metadata => metadata.UpdatedAt).HasColumnType("timestamptz").IsRequired();
    }
}
