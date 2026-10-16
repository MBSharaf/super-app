using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.FeatureFlags</c> — see ERD.</summary>
internal sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable("FeatureFlags", "ops", t =>
        {
            t.HasCheckConstraint("CK_FeatureFlags_Kind", "[Kind] BETWEEN 0 AND 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(80).IsUnicode(false);
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.IsEnabled).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(x => x.Reason).HasMaxLength(400);
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_FeatureFlags_Key");
    }
}
