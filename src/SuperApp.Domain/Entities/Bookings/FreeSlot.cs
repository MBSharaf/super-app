using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>A free hour posted by a venue as a live card. Table <c>booking.FreeSlots</c>.</summary>
public class FreeSlot : BaseEntity
{
    public long PitchId { get; set; }
    public Guid PostedById { get; set; }
    public DateOnly Date { get; set; }
    public byte Hour { get; set; }
    public decimal DiscountPercent { get; set; }
    public long? OfferId { get; set; }
    public long? BookingId { get; set; }
    public FreeSlotStatus Status { get; set; }

    // Navigation
    public Pitch Pitch { get; set; } = null!;
    public User PostedBy { get; set; } = null!;
    public Offer? Offer { get; set; }
    public Booking? Booking { get; set; }
}
