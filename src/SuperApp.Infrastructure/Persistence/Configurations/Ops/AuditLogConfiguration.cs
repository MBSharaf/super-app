using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.AuditLogs</c> — see ERD.</summary>
internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", "ops", t =>
        {
            t.HasCheckConstraint("CK_AuditLogs_System", "[System] BETWEEN 0 AND 6");
            t.HasCheckConstraint("CK_AuditLogs_DataJson_IsJson", "[DataJson] IS NULL OR ISJSON([DataJson]) = 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.At).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.Property(x => x.System).HasConversion<byte>();
        builder.Property(x => x.Action).HasMaxLength(80).IsUnicode(false);
        builder.Property(x => x.EntityType).HasMaxLength(60).IsUnicode(false);
        builder.Property(x => x.EntityId).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.Summary).HasMaxLength(500);
        builder.Property(x => x.Ip).HasMaxLength(45).IsUnicode(false);
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsUnicode(false);
        builder.HasIndex(x => x.At).HasDatabaseName("IX_AuditLogs_At");
        builder.HasIndex(x => new { x.EntityType, x.EntityId }).HasDatabaseName("IX_AuditLogs_EntityType_EntityId");
        builder.HasIndex(x => new { x.ActorUserId, x.At }).HasDatabaseName("IX_AuditLogs_ActorUserId_At");
        builder.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ActorAgent).WithMany().HasForeignKey(x => x.ActorAgentId).OnDelete(DeleteBehavior.NoAction);
    }
}
