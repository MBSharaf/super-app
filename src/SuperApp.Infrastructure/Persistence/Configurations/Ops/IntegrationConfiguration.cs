using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.Integrations</c> — see ERD.</summary>
internal sealed class IntegrationConfiguration : IEntityTypeConfiguration<Integration>
{
    public void Configure(EntityTypeBuilder<Integration> builder)
    {
        builder.ToTable("Integrations", "ops", t =>
        {
            t.HasCheckConstraint("CK_Integrations_Category", "[Category] BETWEEN 0 AND 11");
            t.HasCheckConstraint("CK_Integrations_Mode", "[Mode] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Integrations_ConfigJson_IsJson", "[ConfigJson] IS NULL OR ISJSON([ConfigJson]) = 1");
            t.HasCheckConstraint("CK_Integrations_Status", "[Status] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(60).IsUnicode(false);
        builder.Property(x => x.Category).HasConversion<byte>();
        builder.Property(x => x.Name).HasMaxLength(80);
        builder.Property(x => x.Mode).HasConversion<byte>().HasDefaultValue(IntegrationMode.Simulated).ValueGeneratedNever();
        builder.Property(x => x.IsEnabled).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.IsPrimary).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.SecretName).HasMaxLength(120).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(IntegrationStatus.Unknown).ValueGeneratedNever();
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_Integrations_Key");
        builder.HasIndex(x => x.Category).IsUnique().HasFilter("[IsPrimary] = 1").HasDatabaseName("UQ_Integrations_PrimaryPerCategory");
    }
}
