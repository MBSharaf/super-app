using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.LevelReviews</c> — see ERD.</summary>
internal sealed class LevelReviewConfiguration : IEntityTypeConfiguration<LevelReview>
{
    public void Configure(EntityTypeBuilder<LevelReview> builder)
    {
        builder.ToTable("LevelReviews", "pod", t =>
        {
            t.HasCheckConstraint("CK_LevelReviews_Action", "[Action] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasConversion<byte>();
        builder.Property(x => x.Note).HasMaxLength(400);
        builder.HasIndex(x => new { x.SellerId, x.Month }).IsUnique().HasDatabaseName("UQ_LevelReviews_SellerId_Month");
        builder.HasOne(x => x.Seller).WithMany(p => p.LevelReviews).HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.FromLevel).WithMany().HasForeignKey(x => x.FromLevelId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ToLevel).WithMany().HasForeignKey(x => x.ToLevelId).OnDelete(DeleteBehavior.NoAction);
    }
}
