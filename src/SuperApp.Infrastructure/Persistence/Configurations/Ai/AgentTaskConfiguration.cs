using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ai;

/// <summary>Table <c>ai.AgentTasks</c> — see ERD.</summary>
internal sealed class AgentTaskConfiguration : IEntityTypeConfiguration<AgentTask>
{
    public void Configure(EntityTypeBuilder<AgentTask> builder)
    {
        builder.ToTable("AgentTasks", "ai", t =>
        {
            t.HasCheckConstraint("CK_AgentTasks_TriggerDetails", "([Trigger] <> 0 OR [EventType] IS NOT NULL) AND ([Trigger] <> 1 OR [Cron] IS NOT NULL)");
            t.HasCheckConstraint("CK_AgentTasks_Trigger", "[Trigger] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(120);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Trigger).HasConversion<byte>();
        builder.Property(x => x.EventType).HasMaxLength(80).IsUnicode(false);
        builder.Property(x => x.Cron).HasMaxLength(60).IsUnicode(false);
        builder.Property(x => x.ExpectedOutput).HasMaxLength(1000);
        builder.Property(x => x.IsEnabled).HasDefaultValue(true).ValueGeneratedNever();
        builder.HasOne(x => x.Agent).WithMany(p => p.Tasks).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
    }
}
