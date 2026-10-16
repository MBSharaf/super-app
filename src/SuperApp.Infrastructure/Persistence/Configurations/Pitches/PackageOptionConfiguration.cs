using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pitches;

/// <summary>Table <c>pitch.PackageOptions</c> — see ERD.</summary>
internal sealed class PackageOptionConfiguration : IEntityTypeConfiguration<PackageOption>
{
    public void Configure(EntityTypeBuilder<PackageOption> builder)
    {
        builder.ToTable("PackageOptions", "pitch", t =>
        {
            t.HasCheckConstraint("CK_PackageOptions_MatchCount", "[MatchCount] BETWEEN 1 AND 12");
            t.HasCheckConstraint("CK_PackageOptions_DiscountPercent", "[DiscountPercent] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_PackageOptions_Status", "[Status] BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_PackageOptions_ApprovedDiscount", "[ApprovedDiscount] IS NULL OR [ApprovedDiscount] BETWEEN 0 AND 100");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DiscountPercent).HasPrecision(5, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(PackageOptionStatus.WaitingForApproval).ValueGeneratedNever();
        builder.Property(x => x.ApprovedDiscount).HasPrecision(5, 2);
        builder.Property(x => x.RejectReason).HasMaxLength(400);
        builder.HasIndex(x => new { x.PitchId, x.MatchCount }).IsUnique().HasDatabaseName("UQ_PackageOptions_PitchId_MatchCount");
        builder.HasOne(x => x.Pitch).WithMany(p => p.PackageOptions).HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.NoAction);
    }
}
