using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Auth;

/// <summary>A permission granted directly to a user (e.g. Approvals to one admin). Table <c>auth.UserPermissions</c>.</summary>
public class UserPermission
{
    public Guid UserId { get; set; }
    public int PermissionId { get; set; }
    public Guid GrantedById { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
    public User GrantedBy { get; set; } = null!;
}
