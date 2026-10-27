using Microsoft.Extensions.Options;
using SuperApp.Application.Abstractions.Email;
using SuperApp.Application.Abstractions.Identity;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Options;
using SuperApp.Application.Features.Ceo.Dtos;
using SuperApp.Application.Security;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Ceo.Services;

/// <summary>The CEO's team screen (API §8 "Admins"): create admins by invitation, roles, direct permissions, limits, status.</summary>
public interface IAdminAccountsService
{
    Task<IReadOnlyList<AdminResponse>> ListAsync(CancellationToken ct);

    Task<AdminResponse> GetAsync(Guid id, CancellationToken ct);

    Task<AdminResponse> CreateAsync(CreateAdminRequest request, CancellationToken ct);

    Task<AdminResponse> UpdateAsync(Guid id, UpdateAdminRequest request, CancellationToken ct);

    Task<AdminResponse> SetPermissionsAsync(Guid id, SetPermissionsRequest request, CancellationToken ct);

    Task<AdminResponse> SetLimitsAsync(Guid id, StaffLimitsDto request, CancellationToken ct);

    Task<AdminResponse> SuspendAsync(Guid id, SuspendRequest request, CancellationToken ct);

    Task<AdminResponse> ReactivateAsync(Guid id, CancellationToken ct);

    Task<AdminResponse> ResetTwoFactorAsync(Guid id, CancellationToken ct);

    Task ResendInviteAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<RoleResponse>> RolesAsync(CancellationToken ct);

    /// <summary>A new admin role with its permissions (the CEO's permission matrix).</summary>
    Task<RoleResponse> CreateRoleAsync(RoleRequest request, CancellationToken ct);

    /// <summary>Admin roles only; a locked (seeded) role keeps its name. Holders get the change within a minute.</summary>
    Task<RoleResponse> UpdateRoleAsync(Guid id, RoleRequest request, CancellationToken ct);

    /// <summary>409 <c>role_locked</c> for seeded roles, <c>role_in_use</c> while someone holds it.</summary>
    Task DeleteRoleAsync(Guid id, CancellationToken ct);

    IReadOnlyList<PermissionResponse> PermissionCatalogue();
}

public sealed class AdminAccountsService(
    IStaffStore staff,
    ITwoFactorService twoFactor,
    IEmailService email,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IOptions<SiteOptions> site) : IAdminAccountsService
{
    private static readonly StaffLimitsDto NoLimits = new(0, 0, 0, false);

    public Task<IReadOnlyList<AdminResponse>> ListAsync(CancellationToken ct) => staff.ListAdminsAsync(ct);

    public async Task<AdminResponse> GetAsync(Guid id, CancellationToken ct) =>
        await staff.GetAdminAsync(id, ct) ?? throw new NotFoundException("This admin does not exist.");

    public async Task<AdminResponse> CreateAsync(CreateAdminRequest request, CancellationToken ct)
    {
        if (await staff.EmailExistsAsync(request.Email.Trim(), ct))
        {
            throw new ConflictException("An account with this email already exists.", "email_taken");
        }
        await CheckRolesAsync(request.Roles, ct);
        var id = await staff.CreateAdminAsync(new NewAdmin(request.FullName.Trim(), request.Email.Trim(), request.Roles, request.Language), ct);
        await staff.SetDirectPermissionsAsync(id, request.Permissions ?? [], null, Ceo(), ct);
        await staff.SetLimitsAsync(id, request.Limits ?? NoLimits, ct);
        await SendInviteAsync(id, ct);
        await audit.LogAsync(AuditSystem.Ceo, "admin.created", $"Admin {request.Email} invited ({string.Join(", ", request.Roles)})", "User", id.ToString(), request, ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminResponse> UpdateAsync(Guid id, UpdateAdminRequest request, CancellationToken ct)
    {
        await EnsureEditableAsync(id, ct);
        await CheckRolesAsync(request.Roles, ct);
        await staff.UpdateAdminAsync(id, request.FullName.Trim(), request.Roles, ct);
        await audit.LogAsync(AuditSystem.Ceo, "admin.updated", $"Roles: {string.Join(", ", request.Roles)}", "User", id.ToString(), request, ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminResponse> SetPermissionsAsync(Guid id, SetPermissionsRequest request, CancellationToken ct)
    {
        await EnsureEditableAsync(id, ct);
        await staff.SetDirectPermissionsAsync(id, request.Permissions.Distinct().ToList(), request.ExpiresAt, Ceo(), ct);
        await audit.LogAsync(AuditSystem.Ceo, "admin.permissions", $"Direct permissions: {string.Join(", ", request.Permissions)}", "User", id.ToString(), request, ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminResponse> SetLimitsAsync(Guid id, StaffLimitsDto request, CancellationToken ct)
    {
        await EnsureEditableAsync(id, ct);
        await staff.SetLimitsAsync(id, request, ct);
        await audit.LogAsync(AuditSystem.Ceo, "admin.limits", $"Limits: refund {request.Refund}, credit {request.Credit}, payout {request.Payout}", "User", id.ToString(), request, ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminResponse> SuspendAsync(Guid id, SuspendRequest request, CancellationToken ct)
    {
        await EnsureEditableAsync(id, ct);
        await staff.SetStatusAsync(id, UserStatus.Suspended, request.Reason.Trim(), ct);
        await audit.LogAsync(AuditSystem.Ceo, "admin.suspended", request.Reason, "User", id.ToString(), ct: ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminResponse> ReactivateAsync(Guid id, CancellationToken ct)
    {
        await EnsureEditableAsync(id, ct);
        await staff.SetStatusAsync(id, UserStatus.Active, null, ct);
        await audit.LogAsync(AuditSystem.Ceo, "admin.reactivated", "Account reactivated", "User", id.ToString(), ct: ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdminResponse> ResetTwoFactorAsync(Guid id, CancellationToken ct)
    {
        await EnsureEditableAsync(id, ct);
        await twoFactor.ResetAsync(id);
        await audit.LogAsync(AuditSystem.Ceo, "admin.2fa_reset", "Authenticator cleared — set up again at next sign-in", "User", id.ToString(), ct: ct);
        return await GetAsync(id, ct);
    }

    public async Task ResendInviteAsync(Guid id, CancellationToken ct)
    {
        var admin = await GetAsync(id, ct);
        if (!admin.InvitationPending)
        {
            throw new ConflictException("This admin already accepted the invitation.", "invite_accepted");
        }
        await SendInviteAsync(id, ct);
    }

    public Task<IReadOnlyList<RoleResponse>> RolesAsync(CancellationToken ct) => staff.ListRolesAsync(ct);

    public async Task<RoleResponse> CreateRoleAsync(RoleRequest request, CancellationToken ct)
    {
        await CheckNameAsync(request.Name.Trim(), null, ct);
        var id = await staff.SaveRoleAsync(null, request.Name.Trim(), request.Description?.Trim(), request.Permissions.Distinct().ToList(), ct);
        await audit.LogAsync(AuditSystem.Ceo, "role.created", $"Role {request.Name.Trim()} added ({request.Permissions.Count} permissions)", "Role", id.ToString(), request, ct);
        return (await staff.RoleAsync(id, ct))!;
    }

    public async Task<RoleResponse> UpdateRoleAsync(Guid id, RoleRequest request, CancellationToken ct)
    {
        var role = await AdminRoleAsync(id, ct);
        if (!role.IsLocked)
        {
            await CheckNameAsync(request.Name.Trim(), id, ct);
        }
        await staff.SaveRoleAsync(id, request.Name.Trim(), request.Description?.Trim(), request.Permissions.Distinct().ToList(), ct);
        await audit.LogAsync(AuditSystem.Ceo, "role.updated", $"Role {role.Name}: {string.Join(", ", request.Permissions)}", "Role", id.ToString(), request, ct);
        return (await staff.RoleAsync(id, ct))!;
    }

    public async Task DeleteRoleAsync(Guid id, CancellationToken ct)
    {
        var role = await AdminRoleAsync(id, ct);
        if (role.IsLocked)
        {
            throw new ConflictException("This role comes with the platform and cannot be deleted.", "role_locked");
        }
        if (role.Holders > 0)
        {
            throw new ConflictException("Someone still has this role. Give them another role first.", "role_in_use");
        }
        await staff.DeleteRoleAsync(id, ct);
        await audit.LogAsync(AuditSystem.Ceo, "role.deleted", $"Role {role.Name} deleted", "Role", id.ToString(), ct: ct);
    }

    public IReadOnlyList<PermissionResponse> PermissionCatalogue() =>
        Permissions.All.Select(p => new PermissionResponse(p.Code, p.Area, p.NameEn, p.NameAr, p.IsSensitive)).ToList();

    private async Task SendInviteAsync(Guid id, CancellationToken ct)
    {
        var admin = await GetAsync(id, ct);
        var token = await staff.CreateInviteTokenAsync(id);
        var link = site.Value.Link("auth/invite", ("u", id.ToString()), ("t", token));
        var model = new Dictionary<string, string> { ["name"] = admin.FullName, ["link"] = link, ["roles"] = string.Join(", ", admin.Roles) };
        await email.SendAsync(new EmailMessage(admin.Email, "admin-invite", model, "ar", id), ct);
    }

    /// <summary>The CEO account is never changed from this screen, and nobody edits their own account here.</summary>
    private async Task EnsureEditableAsync(Guid id, CancellationToken ct)
    {
        if (await staff.IsCeoAsync(id, ct) || id == currentUser.UserId)
        {
            throw new ForbiddenException("The CEO account cannot be changed from this screen.", "ceo_protected");
        }
        _ = await GetAsync(id, ct);
    }

    /// <summary>Only admin roles are edited here — never the CEO, customer, partner, seller, factory or agent roles.</summary>
    private async Task<RoleResponse> AdminRoleAsync(Guid id, CancellationToken ct)
    {
        var role = await staff.RoleAsync(id, ct) ?? throw new NotFoundException("This role does not exist.");
        return role.Kind == nameof(RoleKind.AdminRole) ? role
            : throw new ForbiddenException("Only admin roles can be changed here.", "not_admin_role");
    }

    private async Task CheckNameAsync(string name, Guid? id, CancellationToken ct)
    {
        if (await staff.RoleNameTakenAsync(name, id, ct))
        {
            throw new ConflictException("A role with this name already exists.", "name_taken");
        }
    }

    private async Task CheckRolesAsync(IEnumerable<string> roles, CancellationToken ct)
    {
        var allowed = await staff.AdminRoleNamesAsync(ct);
        if (!roles.All(allowed.Contains))
        {
            throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure("roles", "Only admin roles can be given here.")]);
        }
    }

    private Guid Ceo() => currentUser.UserId ?? throw new UnauthorizedException();
}
