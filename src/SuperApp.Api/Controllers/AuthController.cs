using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Auth.Dtos;
using SuperApp.Application.Features.Auth.Services;

namespace SuperApp.Api.Controllers;

/// <summary>Sign-up, sign-in and the emailed links (API §2.1). After sign-in or sign-out, call GET csrf again.</summary>
[Route("api/v1/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(IAuthService auth, IAntiforgery antiforgery) : ApiControllerBase
{
    /// <summary>Anti-forgery token for the site; send it as X-CSRF-TOKEN on every change.</summary>
    [HttpGet("csrf")]
    [DisableRateLimiting]
    public ActionResult<object> Csrf()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfResponse(tokens.RequestToken!, CookieAuthSetup.CsrfHeader));
    }

    [HttpPost("sign-up")]
    public async Task<ActionResult<object>> SignUp(SignUpRequest request, CancellationToken ct) =>
        Created(await auth.SignUpAsync(request, ct), "Your account is ready. Check your email to confirm it.");

    [HttpPost("sign-in")]
    public async Task<ActionResult<object>> SignIn(SignInRequest request, CancellationToken ct)
    {
        var result = await auth.SignInAsync(request, ct);
        return Ok(result, result.Requires2fa ? "Enter the 6-digit code from your authenticator app." : "Welcome back!");
    }

    [HttpPost("sign-out")]
    [Authorize]
    public async Task<IActionResult> SignOutNow(CancellationToken ct)
    {
        await auth.SignOutAsync(ct);
        return NoContent();
    }

    [HttpPost("sign-out-everywhere")]
    [Authorize]
    public async Task<IActionResult> SignOutEverywhere(CancellationToken ct)
    {
        await auth.SignOutEverywhereAsync(ct);
        await auth.SignOutAsync(ct);
        return NoContent();
    }

    [HttpPost("confirm-email")]
    public async Task<ActionResult<object>> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct)
    {
        await auth.ConfirmEmailAsync(request, ct);
        return Ok(new { confirmed = true }, "Your email is confirmed.");
    }

    [HttpPost("resend-confirmation")]
    [Authorize]
    public async Task<ActionResult<object>> ResendConfirmation(CancellationToken ct)
    {
        await auth.ResendConfirmationAsync(ct);
        return Ok(new { sent = true }, "We sent you a new confirmation email.");
    }

    /// <summary>Always 202 — it never tells whether the email has an account.</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await auth.ForgotPasswordAsync(request, ct);
        return Accepted(Application.Common.Responses.ApiResponse.Ok<object>(null, "If this email has an account, a reset link is on its way."));
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<object>> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(request, ct);
        return Ok(new { reset = true }, "Your password is changed. Sign in with the new one.");
    }
}
