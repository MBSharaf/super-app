using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>A customer waiting for a booked hour. Table <c>booking.WaitlistEntries</c>.</summary>
public class WaitlistEntry : BaseEntity
{
    public long PitchId { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public byte Hour { get; set; }
    public WaitlistStatus Status { get; set; }
    public DateTime? NotifiedAt { get; set; }

    // Navigation
    public Pitch Pitch { get; set; } = null!;
    public User User { get; set; } = null!;
}
