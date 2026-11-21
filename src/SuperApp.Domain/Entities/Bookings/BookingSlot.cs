using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>
/// One hour of a booking — makes double booking impossible. Table <c>booking.BookingSlots</c>. On a padel court each row is
/// a half hour and <c>Hour</c> holds its index in the day (hour × 2 + 1 for :30, 0–47); a court is never football too.
/// </summary>
public class BookingSlot : Entity<long>
{
    public long BookingId { get; set; }
    public long PitchId { get; set; }
    public DateOnly Date { get; set; }
    public byte Hour { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Booking Booking { get; set; } = null!;
    public Pitch Pitch { get; set; } = null!;
}
