using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>Table <c>money.ApprovalDecisions</c> — see ERD.</summary>
internal sealed class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> builder)
    {
        builder.ToTable("ApprovalDecisions", "money", t =>
        {
            t.HasCheckConstraint("CK_ApprovalDecisions_Decision", "[Decision] BETWEEN 0 AND 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Decision).HasConversion<byte>();
        builder.Property(x => x.Note).HasMaxLength(400);
        builder.HasIndex(x => new { x.ApprovalId, x.DeciderId }).IsUnique().HasDatabaseName("UQ_ApprovalDecisions_ApprovalId_DeciderId");
        builder.HasOne(x => x.Approval).WithMany(p => p.Decisions).HasForeignKey(x => x.ApprovalId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Decider).WithMany().HasForeignKey(x => x.DeciderId).OnDelete(DeleteBehavior.NoAction);
    }
}
