using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Abstractions.Identity;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Auth.Dtos;
using SuperApp.Application.Features.Auth.Services;

namespace SuperApp.Api.Controllers;

/// <summary>
/// Two-step sign-in (API §2.1). Admins and the CEO: sign in → setup (scan QR) → enable with the first code →
/// keep the recovery codes. Next time: sign in → verify with the app code. After enable / verify, call GET /auth/csrf again.
/// </summary>
[Route("api/v1/auth/2fa")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class TwoFactorController(ITwoFactorService twoFactor, IMeService me, ICurrentUser currentUser) : ApiControllerBase
{
    [HttpPost("setup")]
    [Authorize]
    public async Task<ActionResult<object>> Setup() =>
        Ok(await twoFactor.BeginSetupAsync(Me()), "Scan the QR code with your authenticator app, then enter the 6-digit code.");

    [HttpPost("enable")]
    [Authorize]
    public async Task<ActionResult<object>> Enable(TwoFactorCodeRequest request) =>
        Ok(await twoFactor.EnableAsync(Me(), request.Code), "Two-step sign-in is on. Keep the recovery codes somewhere safe.");

    /// <summary>Second step of sign-in (the pending sign-in is kept in a short-lived cookie).</summary>
    [HttpPost("verify")]
    public async Task<ActionResult<object>> Verify(TwoFactorVerifyRequest request, CancellationToken ct)
    {
        var userId = await twoFactor.VerifyAsync(request.Code, request.IsRecoveryCode, request.Remember);
        return Ok(new SignInResponse(true, false, await me.GetAsync(userId, ct)), "Welcome back!");
    }

    [HttpPost("recovery-codes")]
    [Authorize]
    public async Task<ActionResult<object>> RecoveryCodes(TwoFactorCodeRequest request) =>
        Ok(await twoFactor.NewRecoveryCodesAsync(Me(), request.Code), "New recovery codes — the old ones no longer work.");

    [HttpPost("disable")]
    [Authorize]
    public async Task<IActionResult> Disable(TwoFactorCodeRequest request)
    {
        await twoFactor.DisableAsync(Me(), request.Code);
        return NoContent();
    }

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}
