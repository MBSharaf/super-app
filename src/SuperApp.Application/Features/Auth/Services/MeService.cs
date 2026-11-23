using SuperApp.Application.Abstractions.Identity;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Abstractions.Storage;
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

    /// <summary>Uploads the person's own profile photo (JPEG/PNG/WEBP up to 5 MB, re-encoded without hidden data).</summary>
    Task<MeResponse> SetAvatarAsync(UploadedFile? file, CancellationToken ct);

    Task<MeResponse> RemoveAvatarAsync(CancellationToken ct);
}

public sealed class MeService(IIdentityService identity, ISessionService session, ICurrentUser currentUser, IAuditLogger audit, IFileStorage storage) : IMeService
{
    public async Task<MeResponse> GetAsync(Guid userId, CancellationToken ct)
    {
        var p = await identity.GetProfileAsync(userId, ct);
        return new MeResponse(p.Id, p.FullName, p.Email, p.EmailConfirmed, p.Phone, p.Language, p.Roles, p.Permissions,
            Dashboards(p.Roles, p.Permissions), p.TwoFactorEnabled, p.Permissions.Count > 0 && !p.TwoFactorEnabled, p.CreatedAt,
            p.AvatarFileId is { } avatar ? $"/api/v1/files/{avatar}" : null, p.Player?.Position, p.Player?.Level, p.Player?.Foot, p.Player?.Area);
    }

    public Task<MeResponse> GetCurrentAsync(CancellationToken ct) => GetAsync(Me(), ct);

    public async Task<MeResponse> UpdateAsync(UpdateMeRequest request, CancellationToken ct)
    {
        var userId = Me();
        var phone = request.Phone is null ? null : EgyptianPhone.Normalize(request.Phone);
        await identity.UpdateProfileAsync(userId, request.FullName?.Trim(), phone, request.Language, ct);
        if (request.Position is not null || request.Level is not null || request.Foot is not null || request.Area is not null)
        {
            await identity.UpdatePlayerProfileAsync(userId, new PlayerProfile(request.Position, request.Level, request.Foot, request.Area), ct);
        }
        return await GetAsync(userId, ct);
    }

    public async Task<MeResponse> SetAvatarAsync(UploadedFile? file, CancellationToken ct)
    {
        var userId = Me();
        if (file is null || file.Length == 0)
        {
            throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure("avatar", "Choose a photo.")]);
        }
        var fileId = await storage.SaveAsync(file, UploadKind.Photo, userId, "avatar", ct);
        await identity.SetAvatarAsync(userId, fileId, ct);
        await audit.LogAsync(AuditSystem.Site, "account.avatar_changed", "Profile photo changed", "User", userId.ToString(), ct: ct);
        return await GetAsync(userId, ct);
    }

    public async Task<MeResponse> RemoveAvatarAsync(CancellationToken ct)
    {
        var userId = Me();
        await identity.SetAvatarAsync(userId, null, ct);
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
