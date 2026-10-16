using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.KeyResults</c> — see ERD.</summary>
internal sealed class KeyResultConfiguration : IEntityTypeConfiguration<KeyResult>
{
    public void Configure(EntityTypeBuilder<KeyResult> builder)
    {
        builder.ToTable("KeyResults", "ops", t =>
        {
            t.HasCheckConstraint("CK_KeyResults_Target", "[Target] > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Metric).HasMaxLength(60).IsUnicode(false);
        builder.Property(x => x.Target).HasPrecision(14, 2);
        builder.Property(x => x.Current).HasPrecision(14, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.HasOne(x => x.Goal).WithMany(p => p.KeyResults).HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.Cascade);
    }
}
