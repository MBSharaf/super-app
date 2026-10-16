using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.FreeSlots</c> — see ERD.</summary>
internal sealed class FreeSlotConfiguration : IEntityTypeConfiguration<FreeSlot>
{
    public void Configure(EntityTypeBuilder<FreeSlot> builder)
    {
        builder.ToTable("FreeSlots", "booking", t =>
        {
            t.HasCheckConstraint("CK_FreeSlots_Hour", "[Hour] <= 23");
            t.HasCheckConstraint("CK_FreeSlots_DiscountPercent", "[DiscountPercent] BETWEEN 0 AND 90");
            t.HasCheckConstraint("CK_FreeSlots_Status", "[Status] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DiscountPercent).HasPrecision(5, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(FreeSlotStatus.Live).ValueGeneratedNever();
        builder.HasIndex(x => new { x.PitchId, x.Date, x.Hour }).IsUnique().HasFilter("[Status] = 0").HasDatabaseName("UQ_FreeSlots_LivePerHour");
        builder.HasOne(x => x.Pitch).WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.PostedBy).WithMany().HasForeignKey(x => x.PostedById).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Offer).WithMany().HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.NoAction);
    }
}
