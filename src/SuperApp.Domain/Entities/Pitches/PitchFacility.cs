using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Pitches;

/// <summary>Facilities of a pitch. Table <c>pitch.PitchFacilities</c>.</summary>
public class PitchFacility
{
    public long PitchId { get; set; }
    public int FacilityId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Pitch Pitch { get; set; } = null!;
    public Facility Facility { get; set; } = null!;
}
