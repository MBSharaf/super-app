using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ai;

/// <summary>Table <c>ai.AgentRuns</c> — see ERD.</summary>
internal sealed class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        builder.ToTable("AgentRuns", "ai", t =>
        {
            t.HasCheckConstraint("CK_AgentRuns_Result", "[Result] BETWEEN 0 AND 4");
            t.HasCheckConstraint("CK_AgentRuns_InputTokens", "[InputTokens] >= 0");
            t.HasCheckConstraint("CK_AgentRuns_OutputTokens", "[OutputTokens] >= 0");
            t.HasCheckConstraint("CK_AgentRuns_CostUsd", "[CostUsd] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StartedAt).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.Property(x => x.Result).HasConversion<byte>().HasDefaultValue(AgentRunResult.Running).ValueGeneratedNever();
        builder.Property(x => x.Summary).HasMaxLength(2000);
        builder.Property(x => x.InputTokens).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.OutputTokens).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.CostUsd).HasPrecision(10, 4).HasDefaultValue(0m).ValueGeneratedNever();
        builder.HasIndex(x => new { x.AgentId, x.StartedAt }).HasDatabaseName("IX_AgentRuns_AgentId_StartedAt");
        builder.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.SetNull);
    }
}
