using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Communities;

/// <summary>Table <c>community.ContentReports</c> — see ERD.</summary>
internal sealed class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> builder)
    {
        builder.ToTable("ContentReports", "community", t =>
        {
            t.HasCheckConstraint("CK_ContentReports_OneTarget", "([PostId] IS NULL AND [CommentId] IS NOT NULL) OR ([PostId] IS NOT NULL AND [CommentId] IS NULL)");
            t.HasCheckConstraint("CK_ContentReports_Reason", "[Reason] BETWEEN 0 AND 4");
            t.HasCheckConstraint("CK_ContentReports_Status", "[Status] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasConversion<byte>();
        builder.Property(x => x.Detail).HasMaxLength(400);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(ContentReportStatus.Open).ValueGeneratedNever();
        builder.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Comment).WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.HandledBy).WithMany().HasForeignKey(x => x.HandledById).OnDelete(DeleteBehavior.NoAction);
    }
}
