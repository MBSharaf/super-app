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

    private Guid Ceo() => currentUser.UserId ?? throw new UnauthorizedException();
}
