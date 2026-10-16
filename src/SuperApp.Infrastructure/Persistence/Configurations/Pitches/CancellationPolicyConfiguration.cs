using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pitches;

/// <summary>Table <c>pitch.CancellationPolicies</c> — see ERD.</summary>
internal sealed class CancellationPolicyConfiguration : IEntityTypeConfiguration<CancellationPolicy>
{
    public void Configure(EntityTypeBuilder<CancellationPolicy> builder)
    {
        builder.ToTable("CancellationPolicies", "pitch", t =>
        {
            t.HasCheckConstraint("CK_CancellationPolicies_FullRefundHours", "[FullRefundHours] >= 0");
            t.HasCheckConstraint("CK_CancellationPolicies_HalfRefundHours", "[HalfRefundHours] >= 0 AND [HalfRefundHours] <= [FullRefundHours]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.NameEn).HasMaxLength(40);
        builder.Property(x => x.NameAr).HasMaxLength(40);
        builder.Property(x => x.SummaryEn).HasMaxLength(300);
        builder.Property(x => x.SummaryAr).HasMaxLength(300);
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_CancellationPolicies_Key");
    }
}
