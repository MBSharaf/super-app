using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Bookings;

/// <summary>A venue membership: a team that plays every week and renews. Table <c>booking.Series</c>.</summary>
public class Series : BaseEntity
{
    public long PitchId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public Guid? ContactUserId { get; set; }
    public string? ContactPhone { get; set; }
    public byte DayOfWeek { get; set; }
    public byte StartHour { get; set; }
    public byte Duration { get; set; }
    public bool AutoRenew { get; set; } = true;
    public SeriesStatus Status { get; set; }

    // Navigation
    public Pitch Pitch { get; set; } = null!;
    public User? ContactUser { get; set; }
}
