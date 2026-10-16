using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Support;

namespace SuperApp.Infrastructure.Persistence.Configurations.Support;

/// <summary>Table <c>support.TicketMessages</c> — see ERD.</summary>
internal sealed class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("TicketMessages", "support", t =>
        {
            t.HasCheckConstraint("CK_TicketMessages_Body", "LEN([Body]) >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Body).HasMaxLength(4000);
        builder.Property(x => x.IsInternalNote).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasIndex(x => new { x.TicketId, x.CreatedAt }).HasDatabaseName("IX_TicketMessages_TicketId_CreatedAt");
        builder.HasOne(x => x.Ticket).WithMany(p => p.Messages).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.NoAction);
    }
}
