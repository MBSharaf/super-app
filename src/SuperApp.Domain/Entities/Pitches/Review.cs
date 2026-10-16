using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;

namespace SuperApp.Domain.Entities.Pitches;

/// <summary>A customer's review of a played booking. Table <c>pitch.Reviews</c>.</summary>
public class Review : BaseEntity
{
    public long PitchId { get; set; }
    public long BookingId { get; set; }
    public Guid UserId { get; set; }
    public byte Stars { get; set; }
    public string? Text { get; set; }
    public string? Reply { get; set; }
    public DateTime? RepliedAt { get; set; }
    public bool IsHidden { get; set; }

    // Navigation
    public Pitch Pitch { get; set; } = null!;
    public Booking Booking { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<ReviewReport> Reports { get; set; } = [];
}
