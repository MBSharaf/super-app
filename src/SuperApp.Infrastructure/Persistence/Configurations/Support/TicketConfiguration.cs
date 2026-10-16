using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Entities.Support;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Support;

/// <summary>Table <c>support.Tickets</c> — see ERD.</summary>
internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets", "support", t =>
        {
            t.HasCheckConstraint("CK_Tickets_OpenedFrom", "[OpenedFrom] BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_Tickets_Topic", "[Topic] BETWEEN 0 AND 7");
            t.HasCheckConstraint("CK_Tickets_DisputeAmount", "[DisputeAmount] IS NULL OR [DisputeAmount] > 0");
            t.HasCheckConstraint("CK_Tickets_Priority", "[Priority] BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_Tickets_Status", "[Status] BETWEEN 0 AND 4");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.OpenedFrom).HasConversion<byte>();
        builder.Property(x => x.Topic).HasConversion<byte>();
        builder.Property(x => x.Subject).HasMaxLength(150);
        builder.Property(x => x.IsDispute).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.DisputeAmount).HasPrecision(12, 2);
        builder.Property(x => x.Priority).HasConversion<byte>().HasDefaultValue(TicketPriority.Normal).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(TicketStatus.Open).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Tickets_Code");
        builder.HasIndex(x => new { x.Status, x.Priority, x.CreatedAt }).HasDatabaseName("IX_Tickets_Status_Priority_CreatedAt");
        builder.HasIndex(x => x.OpenedById).HasDatabaseName("IX_Tickets_OpenedById");
        builder.HasOne(x => x.OpenedBy).WithMany().HasForeignKey(x => x.OpenedById).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.AgainstUser).WithMany().HasForeignKey(x => x.AgainstUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.AssignedToAgent).WithMany().HasForeignKey(x => x.AssignedToAgentId).OnDelete(DeleteBehavior.NoAction);
    }
}
