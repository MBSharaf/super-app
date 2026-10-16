using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Pitches;

/// <summary>A pitch facility (lights, parking, showers…). Table <c>pitch.Facilities</c>.</summary>
public class Facility : LookupEntity
{
    public string Key { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}
