using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Auth.Dtos;
using SuperApp.Application.Features.Auth.Services;

namespace SuperApp.Api.Controllers;

/// <summary>The signed-in person's own account (API §2.2).</summary>
[Route("api/v1/me")]
[Authorize]
public sealed class MeController(IMeService me) : ApiControllerBase
{
    /// <summary>Profile, roles, permissions and the dashboards this person can open.</summary>
    [HttpGet]
    public async Task<ActionResult<object>> Get(CancellationToken ct) => Ok(await me.GetCurrentAsync(ct));

    [HttpPatch]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Update(UpdateMeRequest request, CancellationToken ct) =>
        Ok(await me.UpdateAsync(request, ct), "Saved.");

    [HttpPut("password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await me.ChangePasswordAsync(request, ct);
        return NoContent();
    }
}
