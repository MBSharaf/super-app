using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.IntegrationUsage</c> — see ERD.</summary>
internal sealed class IntegrationUsageConfiguration : IEntityTypeConfiguration<IntegrationUsage>
{
    public void Configure(EntityTypeBuilder<IntegrationUsage> builder)
    {
        builder.ToTable("IntegrationUsage", "ops", t =>
        {
            t.HasCheckConstraint("CK_IntegrationUsage_Calls", "[Calls] >= 0");
            t.HasCheckConstraint("CK_IntegrationUsage_Errors", "[Errors] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Calls).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.Errors).HasDefaultValue(0).ValueGeneratedNever();
        builder.HasIndex(x => new { x.IntegrationId, x.Day }).IsUnique().HasDatabaseName("UQ_IntegrationUsage_IntegrationId_Day");
        builder.HasOne(x => x.Integration).WithMany(p => p.Usage).HasForeignKey(x => x.IntegrationId).OnDelete(DeleteBehavior.Cascade);
    }
}
