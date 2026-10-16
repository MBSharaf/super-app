using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.GameRequests</c> — see ERD.</summary>
internal sealed class GameRequestConfiguration : IEntityTypeConfiguration<GameRequest>
{
    public void Configure(EntityTypeBuilder<GameRequest> builder)
    {
        builder.ToTable("GameRequests", "booking", t =>
        {
            t.HasCheckConstraint("CK_GameRequests_Status", "[Status] BETWEEN 0 AND 5");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Position).HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.Message).HasMaxLength(300);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(GameRequestStatus.Waiting).ValueGeneratedNever();
        builder.HasIndex(x => new { x.GameId, x.PlayerId }).IsUnique().HasDatabaseName("UQ_GameRequests_GameId_PlayerId");
        builder.HasOne(x => x.Game).WithMany(p => p.Requests).HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.NoAction);
    }
}
