using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Fantasy;

namespace SuperApp.Api.Controllers.Admin;

/// <summary>Fantasy tools for admins (permission fantasy.manage + 2FA): force a football sync, re-score a gameweek, the API-FOOTBALL usage bar.</summary>
[Route("api/v1/admin/fantasy")]
[RequirePermission("fantasy.manage")]
public sealed class AdminFantasyController(IFantasyAdminService admin) : ApiControllerBase
{
    /// <summary>Runs the sync now (?competition=EGY|EPL, else both). Counts against the provider's daily quota.</summary>
    [HttpPost("sync")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Sync([FromQuery] string? competition, CancellationToken ct) => Ok(await admin.SyncAsync(competition, ct), "Synced.");

    /// <summary>Re-applies the scoring of a finished gameweek (after the provider's corrections). 422 not_finished.</summary>
    [HttpPost("rescore/{gameweek:int}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Rescore(int gameweek, [FromQuery] string competition = "EGY", CancellationToken ct = default) =>
        Ok(new { gameweek = await admin.RescoreAsync(competition, gameweek, ct) }, "Gameweek re-scored.");

    /// <summary>Today's API-FOOTBALL calls / daily limit: green, yellow from 70%, red from 90% ("upgrade the plan").</summary>
    [HttpGet("usage")]
    public async Task<ActionResult<object>> Usage(CancellationToken ct) => Ok(await admin.UsageAsync(ct));
}
