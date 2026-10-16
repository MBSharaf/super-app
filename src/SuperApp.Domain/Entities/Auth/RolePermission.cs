using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Auth;

/// <summary>Permissions bundled in a role. Table <c>auth.RolePermissions</c>.</summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public int PermissionId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
