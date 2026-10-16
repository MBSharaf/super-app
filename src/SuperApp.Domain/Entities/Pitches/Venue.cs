using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Geo;

namespace SuperApp.Domain.Entities.Pitches;

/// <summary>A venue (place) owned by a partner. Table <c>pitch.Venues</c>.</summary>
public class Venue : BaseEntity, ISoftDelete
{
    public long PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AreaId { get; set; }
    public string Address { get; set; } = string.Empty;
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
    public string? Phone { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public Partner Partner { get; set; } = null!;
    public Area Area { get; set; } = null!;
}
