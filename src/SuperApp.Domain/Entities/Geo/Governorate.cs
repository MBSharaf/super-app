using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Geo;

/// <summary>One of Egypt's 27 governorates. Table <c>geo.Governorates</c>.</summary>
public class Governorate : LookupEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;

    // Navigation
    public ICollection<Area> Areas { get; set; } = [];
}
