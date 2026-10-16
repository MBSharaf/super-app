using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.OutboxEvents</c> — see ERD.</summary>
internal sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("OutboxEvents", "ops", t =>
        {
            t.HasCheckConstraint("CK_OutboxEvents_PayloadJson_IsJson", "ISJSON([PayloadJson]) = 1");
            t.HasCheckConstraint("CK_OutboxEvents_Status", "[Status] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventId).HasDefaultValueSql("NEWID()").ValueGeneratedNever();
        builder.Property(x => x.Type).HasMaxLength(80).IsUnicode(false);
        builder.Property(x => x.OccurredAt).HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(OutboxStatus.Pending).ValueGeneratedNever();
        builder.Property(x => x.Attempts).HasDefaultValue(0).ValueGeneratedNever();
        builder.HasIndex(x => x.EventId).IsUnique().HasDatabaseName("UQ_OutboxEvents_EventId");
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt }).HasDatabaseName("IX_OutboxEvents_Status_NextAttemptAt");
    }
}
