using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>A 10-minute hold on an hour while the customer pays. Table <c>booking.SlotHolds</c>.</summary>
public class SlotHold : Entity<long>
{
    public long PitchId { get; set; }
    public DateOnly Date { get; set; }
    public byte Hour { get; set; }
    public Guid UserId { get; set; }
    public DateTime ExpiresAt { get; set; }

    // Navigation
    public Pitch Pitch { get; set; } = null!;
    public User User { get; set; } = null!;
}
