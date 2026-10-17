using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Security;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Identity;

/// <summary>
/// The permission codes a person holds: those of their roles plus direct grants that have not expired
/// (for example "Approvals" given by the CEO to one admin). The CEO holds every code.
/// </summary>
public sealed class PermissionReader(AppDbContext db, IClock clock)
{
    public async Task<IReadOnlyList<string>> ForUserAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        if (roles.Contains(SystemRoles.Ceo))
        {
            return Permissions.All.Select(p => p.Code).ToList();
        }
        var now = clock.UtcNow;
        var fromRoles = db.Set<RolePermission>()
            .Where(rp => db.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == rp.RoleId))
            .Select(rp => rp.Permission.Code);
        var direct = db.Set<UserPermission>()
            .Where(up => up.UserId == userId && (up.ExpiresAt == null || up.ExpiresAt > now))
            .Select(up => up.Permission.Code);
        return (await fromRoles.Union(direct).ToListAsync(ct)).Order().ToList();
    }

    /// <summary>Staff = the CEO or anyone holding at least one admin permission (2FA required, 8-hour sessions).</summary>
    public async Task<bool> IsStaffAsync(Guid userId, CancellationToken ct)
    {
        var roles = await db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.Role.Name!).ToListAsync(ct);
        return roles.Contains(SystemRoles.Ceo) || (await ForUserAsync(userId, roles, ct)).Count > 0;
    }
}
