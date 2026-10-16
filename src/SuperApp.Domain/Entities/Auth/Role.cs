using Microsoft.AspNetCore.Identity;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Auth;

/// <summary>A named bundle of permissions (ASP.NET Identity role). Table <c>auth.Roles</c>.</summary>
public class Role : IdentityRole<Guid>
{
    public RoleKind Kind { get; set; }
    public string? DescriptionEn { get; set; }

    /// <summary>System roles (CEO, Customer, PitchOwner…) cannot be deleted.</summary>
    public bool IsLocked { get; set; }

    // Navigation
    public ICollection<UserRole> UserRoles { get; set; } = [];
}
