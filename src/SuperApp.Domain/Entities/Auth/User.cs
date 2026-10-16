using Microsoft.AspNetCore.Identity;
using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Auth;

/// <summary>
/// Every person and service account (ASP.NET Identity user). Table <c>auth.Users</c>.
/// Roles and permissions decide what the account can do; partner, seller and factory profiles hang off it.
/// </summary>
public class User : IdentityUser<Guid>, ITimestamped, ISoftDelete
{
    public string FullName { get; set; } = string.Empty;
    public long? AvatarFileId { get; set; }
    public string Language { get; set; } = "ar";
    public AccountType AccountType { get; set; }
    public UserStatus Status { get; set; }
    public string? SuspendedReason { get; set; }
    public PrepayRule MustPrepay { get; set; }
    public int CodRefusals { get; set; }
    public string? AdminNote { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public StoredFile? AvatarFile { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = [];
}
