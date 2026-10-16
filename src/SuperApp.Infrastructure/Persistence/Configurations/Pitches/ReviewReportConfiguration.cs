using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pitches;

/// <summary>Table <c>pitch.ReviewReports</c> — see ERD.</summary>
internal sealed class ReviewReportConfiguration : IEntityTypeConfiguration<ReviewReport>
{
    public void Configure(EntityTypeBuilder<ReviewReport> builder)
    {
        builder.ToTable("ReviewReports", "pitch", t =>
        {
            t.HasCheckConstraint("CK_ReviewReports_Status", "[Status] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(300);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(ReviewReportStatus.Open).ValueGeneratedNever();
        builder.HasOne(x => x.Review).WithMany(p => p.Reports).HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.NoAction);
    }
}
