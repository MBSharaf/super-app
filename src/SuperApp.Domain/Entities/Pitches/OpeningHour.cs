using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Pitches;

/// <summary>Opening hours of a pitch on one weekday. Table <c>pitch.OpeningHours</c>.</summary>
public class OpeningHour : Entity<long>
{
    public long PitchId { get; set; }
    public byte DayOfWeek { get; set; }
    public byte OpenHour { get; set; }
    public byte CloseHour { get; set; }
    public bool IsClosed { get; set; }

    // Navigation
    public Pitch Pitch { get; set; } = null!;
}
