using SuperApp.Application.Features.Auth.Dtos;

namespace SuperApp.Application.Abstractions.Identity;

/// <summary>
/// Two-step sign-in with an authenticator app (Google / Microsoft Authenticator). Mandatory for admins and
/// the CEO (PRD FR-AUTH-05), optional for customers. Implemented with ASP.NET Identity in Infrastructure.
/// </summary>
public interface ITwoFactorService
{
    /// <summary>A new authenticator key with its QR code; 2FA is not on until <see cref="EnableAsync"/>.</summary>
    Task<TwoFactorSetupResponse> BeginSetupAsync(Guid userId);

    /// <summary>Checks the first code, turns 2FA on, marks this session as verified and returns 10 recovery codes (shown once).</summary>
    Task<RecoveryCodesResponse> EnableAsync(Guid userId, string code);

    /// <summary>Second step of a pending sign-in, with an app code or a recovery code.</summary>
    Task<Guid> VerifyAsync(string code, bool isRecoveryCode, bool remember);

    Task<RecoveryCodesResponse> NewRecoveryCodesAsync(Guid userId, string code);

    /// <summary>Turns 2FA off (customers only — staff must keep it).</summary>
    Task DisableAsync(Guid userId, string code);

    /// <summary>The CEO clears an admin's authenticator (lost phone); the admin sets it up again at next sign-in.</summary>
    Task ResetAsync(Guid userId);
}
