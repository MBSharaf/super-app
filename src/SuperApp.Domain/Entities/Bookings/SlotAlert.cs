using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>A customer's alert for free slots at a pitch or in an area. Table <c>booking.SlotAlerts</c>.</summary>
public class SlotAlert : BaseEntity
{
    public Guid UserId { get; set; }
    public long? PitchId { get; set; }
    public int? AreaId { get; set; }
    public byte? FromHour { get; set; }
    public byte? ToHour { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public User User { get; set; } = null!;
    public Pitch? Pitch { get; set; }
    public Area? Area { get; set; }
}
