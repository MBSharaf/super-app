using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.BookingSlots</c> — see ERD.</summary>
internal sealed class BookingSlotConfiguration : IEntityTypeConfiguration<BookingSlot>
{
    public void Configure(EntityTypeBuilder<BookingSlot> builder)
    {
        builder.ToTable("BookingSlots", "booking", t =>
        {
            t.HasCheckConstraint("CK_BookingSlots_Hour", "[Hour] <= 23");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.HasIndex(x => new { x.PitchId, x.Date, x.Hour }).IsUnique().HasFilter("[IsActive] = 1").HasDatabaseName("UQ_BookingSlots_PitchId_Date_Hour");
        builder.HasOne(x => x.Booking).WithMany(p => p.Slots).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Pitch).WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.NoAction);
    }
}
