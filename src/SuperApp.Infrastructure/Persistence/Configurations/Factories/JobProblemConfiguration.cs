using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Factories;

/// <summary>Table <c>factory.JobProblems</c> — see ERD.</summary>
internal sealed class JobProblemConfiguration : IEntityTypeConfiguration<JobProblem>
{
    public void Configure(EntityTypeBuilder<JobProblem> builder)
    {
        builder.ToTable("JobProblems", "factory", t =>
        {
            t.HasCheckConstraint("CK_JobProblems_Kind", "[Kind] BETWEEN 0 AND 4");
            t.HasCheckConstraint("CK_JobProblems_Status", "[Status] BETWEEN 0 AND 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.Detail).HasMaxLength(400);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(JobProblemStatus.Open).ValueGeneratedNever();
        builder.Property(x => x.Resolution).HasMaxLength(400);
        builder.HasOne(x => x.Job).WithMany(p => p.Problems).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ReportedByStaff).WithMany().HasForeignKey(x => x.ReportedByStaffId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ResolvedBy).WithMany().HasForeignKey(x => x.ResolvedById).OnDelete(DeleteBehavior.NoAction);
    }
}
