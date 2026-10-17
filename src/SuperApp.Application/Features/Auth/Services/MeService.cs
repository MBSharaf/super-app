using SuperApp.Application.Abstractions.Identity;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Validation;
using SuperApp.Application.Features.Auth.Dtos;
using SuperApp.Application.Security;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Auth.Services;

/// <summary>The signed-in person's own account (API §2.2): profile, password.</summary>
public interface IMeService
{
    Task<MeResponse> GetAsync(Guid userId, CancellationToken ct);

    Task<MeResponse> GetCurrentAsync(CancellationToken ct);

    Task<MeResponse> UpdateAsync(UpdateMeRequest request, CancellationToken ct);

    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct);
}

public sealed class MeService(IIdentityService identity, ISessionService session, ICurrentUser currentUser, IAuditLogger audit) : IMeService
{
    public async Task<MeResponse> GetAsync(Guid userId, CancellationToken ct)
    {
        var p = await identity.GetProfileAsync(userId, ct);
        return new MeResponse(p.Id, p.FullName, p.Email, p.EmailConfirmed, p.Phone, p.Language, p.Roles, p.Permissions,
            Dashboards(p.Roles, p.Permissions), p.TwoFactorEnabled, p.Permissions.Count > 0 && !p.TwoFactorEnabled, p.CreatedAt);
    }

    public Task<MeResponse> GetCurrentAsync(CancellationToken ct) => GetAsync(Me(), ct);

    public async Task<MeResponse> UpdateAsync(UpdateMeRequest request, CancellationToken ct)
    {
        var userId = Me();
        var phone = request.Phone is null ? null : EgyptianPhone.Normalize(request.Phone);
        await identity.UpdateProfileAsync(userId, request.FullName?.Trim(), phone, request.Language, ct);
        return await GetAsync(userId, ct);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = Me();
        await identity.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        // Other devices are signed out; this one gets a fresh cookie
        await session.SignOutEverywhereAsync(userId);
        await session.PasswordSignInAsync(userId, request.NewPassword, remember: true);
        await audit.LogAsync(AuditSystem.Site, "account.password_changed", "Password changed", "User", userId.ToString(), ct: ct);
    }

    /// <summary>Which systems the person can open; the site builds its menus from this list.</summary>
    public static IReadOnlyList<string> Dashboards(IReadOnlyList<string> roles, IReadOnlyList<string> permissions)
    {
        var list = new List<string> { "site" };
        if (roles.Contains(SystemRoles.PitchOwner)) { list.Add("partner"); }
        if (roles.Contains(SystemRoles.Seller)) { list.Add("seller"); }
        if (roles.Contains(SystemRoles.FactoryOwner) || roles.Contains(SystemRoles.FactoryStaff)) { list.Add("factory"); }
        if (permissions.Count > 0) { list.Add("admin"); }
        if (roles.Contains(SystemRoles.Ceo)) { list.Add("ceo"); }
        return list;
    }

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}
