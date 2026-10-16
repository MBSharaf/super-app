using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>One hour of a booking — makes double booking impossible. Table <c>booking.BookingSlots</c>.</summary>
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
