using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Identity;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Ceo.Dtos;
using SuperApp.Application.Security;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Identity;

/// <summary>
/// Team accounts in the database. An "admin" is a person (not an AI agent, not the CEO) who holds an admin role
/// or a direct permission. Role and permission changes reach open sessions at their next refresh (within a minute);
/// suspension changes the security stamp, which ends every open session.
/// </summary>
public sealed class StaffStore(AppDbContext db, UserManager<User> users, RoleManager<Role> roleManager, PermissionReader permissions, IClock clock) : IStaffStore
{
    public const string InviteProvider = "Invite";
    private const string InvitePurpose = "admin-invite";

    public async Task<IReadOnlyList<AdminResponse>> ListAdminsAsync(CancellationToken ct)
    {
        var ids = await AdminIdsQuery().ToListAsync(ct);
        var list = new List<AdminResponse>();
        foreach (var id in ids)
        {
            list.Add((await GetAdminAsync(id, ct))!);
        }
        return list.OrderBy(a => a.FullName).ToList();
    }

    public async Task<AdminResponse?> GetAdminAsync(Guid userId, CancellationToken ct)
    {
        if (!await AdminIdsQuery().ContainsAsync(userId, ct))
        {
            return null;
        }
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        var roleNames = await db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.Role.Name!).OrderBy(n => n).ToListAsync(ct);
        var direct = await db.Set<UserPermission>().Where(up => up.UserId == userId)
            .Select(up => new DirectGrantDto(up.Permission.Code, up.ExpiresAt)).ToListAsync(ct);
        var limit = await db.Set<StaffLimit>().FirstOrDefaultAsync(l => l.UserId == userId, ct);
        var limits = limit is null ? new StaffLimitsDto(0, 0, 0, false) : new StaffLimitsDto(limit.RefundLimit, limit.CreditLimit, limit.PayoutLimit, limit.CanSecondApprove);
        return new AdminResponse(user.Id, user.FullName, user.Email!, roleNames, direct, await permissions.ForUserAsync(userId, roleNames, ct), limits,
            user.Status.ToString(), user.SuspendedReason, user.TwoFactorEnabled, user.PasswordHash is null, user.LastSeenAt, user.CreatedAt);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct) => await users.FindByEmailAsync(email) is not null;

    public Task<bool> IsCeoAsync(Guid userId, CancellationToken ct) =>
        db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.Role.Name == SystemRoles.Ceo, ct);

    public async Task<Guid> CreateAdminAsync(NewAdmin admin, CancellationToken ct)
    {
        var user = new User
        {
            UserName = admin.Email, Email = admin.Email, FullName = admin.FullName, Language = admin.Language,
            AccountType = AccountType.Person, Status = UserStatus.Active,
        };
        IdentityErrors.Check(await users.CreateAsync(user), "email");
        IdentityErrors.Check(await users.AddToRolesAsync(user, admin.Roles), "roles");
        return user.Id;
    }

    public async Task UpdateAdminAsync(Guid userId, string fullName, IReadOnlyList<string> roles, CancellationToken ct)
    {
        var user = await RequireAsync(userId);
        user.FullName = fullName;
        var adminRoles = await AdminRoleNamesAsync(ct);
        var current = (await users.GetRolesAsync(user)).Where(adminRoles.Contains).ToList();
        IdentityErrors.Check(await users.RemoveFromRolesAsync(user, current.Except(roles)), "roles");
        IdentityErrors.Check(await users.AddToRolesAsync(user, roles.Except(current)), "roles");
    }

    public async Task SetDirectPermissionsAsync(Guid userId, IReadOnlyList<string> codes, DateTime? expiresAt, Guid grantedBy, CancellationToken ct)
    {
        var existing = await db.Set<UserPermission>().Where(up => up.UserId == userId).ToListAsync(ct);
        db.Set<UserPermission>().RemoveRange(existing);
        var ids = await db.Set<Permission>().Where(p => codes.Contains(p.Code)).Select(p => p.Id).ToListAsync(ct);
        db.Set<UserPermission>().AddRange(ids.Select(id => new UserPermission { UserId = userId, PermissionId = id, GrantedById = grantedBy, ExpiresAt = expiresAt }));
        await db.SaveChangesAsync(ct);
    }

    public async Task SetLimitsAsync(Guid userId, StaffLimitsDto limits, CancellationToken ct)
    {
        var row = await db.Set<StaffLimit>().FirstOrDefaultAsync(l => l.UserId == userId, ct);
        if (row is null)
        {
            row = new StaffLimit { UserId = userId };
            db.Set<StaffLimit>().Add(row);
        }
        (row.RefundLimit, row.CreditLimit, row.PayoutLimit, row.CanSecondApprove) = (limits.Refund, limits.Credit, limits.Payout, limits.CanSecondApprove);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetStatusAsync(Guid userId, UserStatus status, string? reason, CancellationToken ct)
    {
        var user = await RequireAsync(userId);
        user.Status = status;
        user.SuspendedReason = reason;
        IdentityErrors.Check(await users.UpdateAsync(user));
        await users.UpdateSecurityStampAsync(user);
    }

    public async Task<string> CreateInviteTokenAsync(Guid userId) =>
        await users.GenerateUserTokenAsync(await RequireAsync(userId), InviteProvider, InvitePurpose);

    public async Task AcceptInviteAsync(Guid userId, string token, string password)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || user.PasswordHash is not null || !await users.VerifyUserTokenAsync(user, InviteProvider, InvitePurpose, token))
        {
            throw new BadRequestException("This invitation has expired or was already used. Ask the CEO for a new one.", "link_expired");
        }
        IdentityErrors.Check(await users.AddPasswordAsync(user, password), "password");
        user.EmailConfirmed = true;
        user.LastSeenAt = clock.UtcNow;
        IdentityErrors.Check(await users.UpdateAsync(user));
    }

    public async Task<IReadOnlyList<RoleResponse>> ListRolesAsync(CancellationToken ct)
    {
        var all = await roleManager.Roles.OrderBy(r => r.Name).ToListAsync(ct);
        var codes = await db.Set<RolePermission>().Select(rp => new { rp.RoleId, rp.Permission.Code }).ToListAsync(ct);
        var holders = await db.UserRoles.GroupBy(ur => ur.RoleId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return all.Select(r => new RoleResponse(r.Id, r.Name!, r.DescriptionEn, r.Kind.ToString(), r.IsLocked,
            r.Name == SystemRoles.Ceo ? Permissions.All.Select(p => p.Code).ToList() : codes.Where(c => c.RoleId == r.Id).Select(c => c.Code).Order().ToList(),
            holders.GetValueOrDefault(r.Id))).ToList();
    }

    public async Task<IReadOnlySet<string>> AdminRoleNamesAsync(CancellationToken ct) =>
        (await roleManager.Roles.Where(r => r.Kind == RoleKind.AdminRole).Select(r => r.Name!).ToListAsync(ct)).ToHashSet();

    public async Task<RoleResponse?> RoleAsync(Guid id, CancellationToken ct) => (await ListRolesAsync(ct)).FirstOrDefault(r => r.Id == id);

    public Task<bool> RoleNameTakenAsync(string name, Guid? except, CancellationToken ct)
    {
        var normalized = roleManager.NormalizeKey(name);
        return roleManager.Roles.AnyAsync(r => r.NormalizedName == normalized && r.Id != except, ct);
    }

    public async Task<Guid> SaveRoleAsync(Guid? id, string name, string? description, IReadOnlyList<string> permissions, CancellationToken ct)
    {
        var role = id is { } existing ? await roleManager.FindByIdAsync(existing.ToString()) ?? throw new NotFoundException("This role does not exist.")
            : new Role { Kind = RoleKind.AdminRole };
        (role.Name, role.DescriptionEn) = (role.IsLocked ? role.Name : name, description);
        IdentityErrors.Check(id is null ? await roleManager.CreateAsync(role) : await roleManager.UpdateAsync(role), "name");
        var old = await db.Set<RolePermission>().Where(rp => rp.RoleId == role.Id).ToListAsync(ct);
        db.Set<RolePermission>().RemoveRange(old);
        var ids = await db.Set<Permission>().Where(p => permissions.Contains(p.Code)).Select(p => p.Id).ToListAsync(ct);
        db.Set<RolePermission>().AddRange(ids.Select(pid => new RolePermission { RoleId = role.Id, PermissionId = pid }));
        await db.SaveChangesAsync(ct);
        return role.Id;
    }

    public async Task DeleteRoleAsync(Guid id, CancellationToken ct)
    {
        var role = await roleManager.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("This role does not exist.");
        await db.Set<RolePermission>().Where(rp => rp.RoleId == id).ExecuteDeleteAsync(ct);
        IdentityErrors.Check(await roleManager.DeleteAsync(role));
    }

    /// <summary>People with an admin role or a direct permission — not the CEO, not AI agents.</summary>
    private IQueryable<Guid> AdminIdsQuery()
    {
        return db.Users
            .Where(u => u.AccountType == AccountType.Person)
            .Where(u => !db.UserRoles.Any(ur => ur.UserId == u.Id && ur.Role.Name == SystemRoles.Ceo))
            .Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.Role.Kind == RoleKind.AdminRole) || db.Set<UserPermission>().Any(up => up.UserId == u.Id))
            .Select(u => u.Id);
    }

    private async Task<User> RequireAsync(Guid userId) =>
        await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("This admin does not exist.");
}
