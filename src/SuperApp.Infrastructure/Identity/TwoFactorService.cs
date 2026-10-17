using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using QRCoder;
using SuperApp.Application.Abstractions.Identity;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Auth.Dtos;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Identity;

/// <summary>Authenticator-app 2FA with ASP.NET Identity (TOTP, 6 digits, 30 seconds) and 10 single-use recovery codes.</summary>
public sealed class TwoFactorService(
    UserManager<User> users,
    SignInManager<User> signIn,
    PermissionReader permissions,
    IAuditLogger audit) : ITwoFactorService
{
    private const string Issuer = "Super App";

    public async Task<TwoFactorSetupResponse> BeginSetupAsync(Guid userId)
    {
        var user = await RequireAsync(userId);
        if (user.TwoFactorEnabled)
        {
            throw new ConflictException("Two-step sign-in is already on.", "2fa_already_on");
        }
        await users.ResetAuthenticatorKeyAsync(user);
        await signIn.RefreshSignInAsync(user);   // a new key changes the security stamp; keep this session valid
        var key = (await users.GetAuthenticatorKeyAsync(user))!;
        var uri = $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(user.Email!)}?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";
        using var qr = new QRCodeGenerator().CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        var svg = new SvgQRCode(qr).GetGraphic(5);
        return new TwoFactorSetupResponse(Group(key), uri, svg);
    }

    public async Task<RecoveryCodesResponse> EnableAsync(Guid userId, string code)
    {
        var user = await RequireAsync(userId);
        await CheckCodeAsync(user, code);
        await users.SetTwoFactorEnabledAsync(user, true);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        // This session is now verified: sign in again with the 2FA mark
        await signIn.SignInWithClaimsAsync(user, isPersistent: true, [new Claim(AppClaims.AuthMethod, "mfa")]);
        await audit.LogAsync(AuditSystem.Site, "account.2fa_enabled", "Two-step sign-in turned on", "User", userId.ToString());
        return new RecoveryCodesResponse(codes!.ToList());
    }

    public async Task<Guid> VerifyAsync(string code, bool isRecoveryCode, bool remember)
    {
        var user = await signIn.GetTwoFactorAuthenticationUserAsync()
            ?? throw new UnauthorizedException("Your sign-in expired. Please enter your email and password again.", "2fa_expired");
        var result = isRecoveryCode
            ? await signIn.TwoFactorRecoveryCodeSignInAsync(code.Replace(" ", "", StringComparison.Ordinal))
            : await signIn.TwoFactorAuthenticatorSignInAsync(Clean(code), remember, rememberClient: false);
        if (result.IsLockedOut)
        {
            throw new LockedException();
        }
        if (!result.Succeeded)
        {
            throw new UnauthorizedException("The code is not correct. Check the time on your phone and try again.", "wrong_code");
        }
        return user.Id;
    }

    public async Task<RecoveryCodesResponse> NewRecoveryCodesAsync(Guid userId, string code)
    {
        var user = await RequireAsync(userId);
        await CheckCodeAsync(user, code);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await audit.LogAsync(AuditSystem.Site, "account.2fa_recovery_codes", "New recovery codes made", "User", userId.ToString());
        return new RecoveryCodesResponse(codes!.ToList());
    }

    public async Task DisableAsync(Guid userId, string code)
    {
        var user = await RequireAsync(userId);
        if (await permissions.IsStaffAsync(userId, CancellationToken.None))
        {
            throw new ForbiddenException("Team accounts must keep two-step sign-in on.", "2fa_required_for_staff");
        }
        await CheckCodeAsync(user, code);
        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user);
        await audit.LogAsync(AuditSystem.Site, "account.2fa_disabled", "Two-step sign-in turned off", "User", userId.ToString());
    }

    public async Task ResetAsync(Guid userId)
    {
        var user = await RequireAsync(userId);
        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user);   // also changes the security stamp: open sessions end
    }

    private async Task CheckCodeAsync(User user, string code)
    {
        var ok = await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, Clean(code));
        if (!ok)
        {
            throw new BadRequestException("The code is not correct. Check the time on your phone and try again.", "wrong_code");
        }
    }

    private async Task<User> RequireAsync(Guid userId) =>
        await users.FindByIdAsync(userId.ToString()) ?? throw new UnauthorizedException();

    private static string Clean(string code) => code.Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);

    /// <summary>The key in groups of four, easier to type: "abcd efgh …".</summary>
    private static string Group(string key) =>
        string.Join(' ', Enumerable.Range(0, (key.Length + 3) / 4).Select(i => key.Substring(i * 4, Math.Min(4, key.Length - (i * 4))))).ToLower(CultureInfo.InvariantCulture);
}
