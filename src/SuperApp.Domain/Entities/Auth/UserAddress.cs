using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Geo;

namespace SuperApp.Domain.Entities.Auth;

/// <summary>A saved delivery address. Table <c>auth.UserAddresses</c>.</summary>
public class UserAddress : BaseEntity
{
    public Guid UserId { get; set; }
    public int AreaId { get; set; }
    public string Label { get; set; } = "Home";
    public string Street { get; set; } = string.Empty;
    public string? Building { get; set; }
    public string? Floor { get; set; }
    public string? Apartment { get; set; }
    public string? Landmark { get; set; }
    public string Phone { get; set; } = string.Empty;
    public bool IsDefault { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Area Area { get; set; } = null!;
}
