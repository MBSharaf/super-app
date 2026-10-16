using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.Goals</c> — see ERD.</summary>
internal sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("Goals", "ops", t =>
        {
            t.HasCheckConstraint("CK_Goals_Quarter", "[Quarter] LIKE '[0-9][0-9][0-9][0-9]-Q[1-4]'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Quarter).HasMaxLength(7).IsUnicode(false).IsFixedLength();
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
