using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Ceo.Dtos;
using SuperApp.Application.Features.Ceo.Services;

namespace SuperApp.Api.Controllers.Ceo;

/// <summary>The CEO's team (API §8 "Admins"). CEO only, with a 2FA session; every change is audited.</summary>
[Route("api/v1/ceo/admins")]
[Authorize(Policy = Policies.Ceo)]
[EnableRateLimiting(RateLimitPolicies.Write)]
public sealed class CeoAdminsController(IAdminAccountsService admins) : ApiControllerBase
{
    [HttpGet]
    [DisableRateLimiting]
    public async Task<ActionResult<object>> List(CancellationToken ct) => Ok(await admins.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [DisableRateLimiting]
    public async Task<ActionResult<object>> Get(Guid id, CancellationToken ct) => Ok(await admins.GetAsync(id, ct));

    /// <summary>Creates the admin and emails the invitation.</summary>
    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateAdminRequest request, CancellationToken ct) =>
        Created(await admins.CreateAsync(request, ct), "Admin added. The invitation email is on its way.");

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<object>> Update(Guid id, UpdateAdminRequest request, CancellationToken ct) =>
        Ok(await admins.UpdateAsync(id, request, ct), "Saved.");

    /// <summary>Direct permissions for this one admin — for example the Approvals permissions.</summary>
    [HttpPut("{id:guid}/permissions")]
    public async Task<ActionResult<object>> Permissions(Guid id, SetPermissionsRequest request, CancellationToken ct) =>
        Ok(await admins.SetPermissionsAsync(id, request, ct), "Permissions saved.");

    [HttpPut("{id:guid}/limits")]
    public async Task<ActionResult<object>> Limits(Guid id, StaffLimitsDto request, CancellationToken ct) =>
        Ok(await admins.SetLimitsAsync(id, request, ct), "Money limits saved.");

    [HttpPost("{id:guid}/suspend")]
    public async Task<ActionResult<object>> Suspend(Guid id, SuspendRequest request, CancellationToken ct) =>
        Ok(await admins.SuspendAsync(id, request, ct), "Account suspended and signed out.");

    [HttpPost("{id:guid}/reactivate")]
    public async Task<ActionResult<object>> Reactivate(Guid id, CancellationToken ct) =>
        Ok(await admins.ReactivateAsync(id, ct), "Account active again.");

    [HttpPost("{id:guid}/reset-2fa")]
    public async Task<ActionResult<object>> ResetTwoFactor(Guid id, CancellationToken ct) =>
        Ok(await admins.ResetTwoFactorAsync(id, ct), "Authenticator cleared. The admin sets it up again at the next sign-in.");

    [HttpPost("{id:guid}/resend-invite")]
    public async Task<ActionResult<object>> ResendInvite(Guid id, CancellationToken ct)
    {
        await admins.ResendInviteAsync(id, ct);
        return Ok(new { sent = true }, "Invitation sent again.");
    }
}
