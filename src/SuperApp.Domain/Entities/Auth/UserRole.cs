using Microsoft.AspNetCore.Identity;

namespace SuperApp.Domain.Entities.Auth;

/// <summary>Junction: a user holds a role. Table <c>auth.UserRoles</c> (ERD C1).</summary>
public class UserRole : IdentityUserRole<Guid>
{
    public DateTime CreatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
