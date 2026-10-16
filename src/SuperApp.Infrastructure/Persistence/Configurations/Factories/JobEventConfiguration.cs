using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Factories;

namespace SuperApp.Infrastructure.Persistence.Configurations.Factories;

/// <summary>Table <c>factory.JobEvents</c> — see ERD.</summary>
internal sealed class JobEventConfiguration : IEntityTypeConfiguration<JobEvent>
{
    public void Configure(EntityTypeBuilder<JobEvent> builder)
    {
        builder.ToTable("JobEvents", "factory", t =>
        {
            t.HasCheckConstraint("CK_JobEvents_FromStage", "[FromStage] <= 6");
            t.HasCheckConstraint("CK_JobEvents_ToStage", "[ToStage] <= 6");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Note).HasMaxLength(300);
        builder.Property(x => x.At).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.HasOne(x => x.Job).WithMany(p => p.Events).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.NoAction);
    }
}
