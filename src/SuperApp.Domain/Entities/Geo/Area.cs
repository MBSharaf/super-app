using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Geo;

/// <summary>An area inside a governorate. Table <c>geo.Areas</c>.</summary>
public class Area : LookupEntity
{
    public int GovernorateId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }

    // Navigation
    public Governorate Governorate { get; set; } = null!;
}
