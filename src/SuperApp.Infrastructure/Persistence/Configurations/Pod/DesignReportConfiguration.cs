using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.DesignReports</c> — see ERD.</summary>
internal sealed class DesignReportConfiguration : IEntityTypeConfiguration<DesignReport>
{
    public void Configure(EntityTypeBuilder<DesignReport> builder)
    {
        builder.ToTable("DesignReports", "pod", t =>
        {
            t.HasCheckConstraint("CK_DesignReports_Reason", "[Reason] BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_DesignReports_Status", "[Status] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasConversion<byte>();
        builder.Property(x => x.Detail).HasMaxLength(600);
        builder.Property(x => x.Contact).HasMaxLength(120);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(DesignReportStatus.Open).ValueGeneratedNever();
        builder.HasOne(x => x.Design).WithMany().HasForeignKey(x => x.DesignId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ReporterUser).WithMany().HasForeignKey(x => x.ReporterUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.HandledBy).WithMany().HasForeignKey(x => x.HandledById).OnDelete(DeleteBehavior.NoAction);
    }
}
