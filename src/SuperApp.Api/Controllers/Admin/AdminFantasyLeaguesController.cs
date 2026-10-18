using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Fantasy;

namespace SuperApp.Api.Controllers.Admin;

/// <summary>
/// Sponsors, official and paid groups with their prizes, and winners → shop vouchers (permission fantasy.manage + 2FA;
/// paid groups also fantasy.groups.paid, and 423 until payments are live). Never cash prizes.
/// </summary>
[Route("api/v1/admin/fantasy")]
[RequirePermission("fantasy.manage")]
public sealed class AdminFantasyLeaguesController(IFantasyLeaguesAdminService admin) : ApiControllerBase
{
    [HttpGet("sponsors")]
    public async Task<ActionResult<object>> Sponsors(CancellationToken ct) => Ok(await admin.SponsorsAsync(ct));

    [HttpPost("sponsors")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> AddSponsor(SponsorRequest request, CancellationToken ct) => Created(await admin.SaveSponsorAsync(null, request, ct), "Sponsor added.");

    [HttpPut("sponsors/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> EditSponsor(long id, SponsorRequest request, CancellationToken ct) => Ok(await admin.SaveSponsorAsync(id, request, ct), "Sponsor saved.");

    [HttpDelete("sponsors/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> DeleteSponsor(long id, CancellationToken ct)
    {
        await admin.DeleteSponsorAsync(id, ct);
        return Ok<object?>(null, "Sponsor removed.");
    }

    /// <summary>Official and paid groups of the current seasons (?competition=EGY|EPL).</summary>
    [HttpGet("groups")]
    public async Task<ActionResult<object>> Groups([FromQuery] string? competition, CancellationToken ct) => Ok(await admin.GroupsAsync(competition, ct));

    /// <summary>An official sponsor league (prize text and value) or a paid group.</summary>
    [HttpPost("groups")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> AddGroup(AdminGroupRequest request, CancellationToken ct) => Created(await admin.SaveGroupAsync(null, request, ct), "Group created.");

    /// <summary>Edit a group and its prize.</summary>
    [HttpPut("groups/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> EditGroup(long id, AdminGroupRequest request, CancellationToken ct) => Ok(await admin.SaveGroupAsync(id, request, ct), "Group saved.");

    /// <summary>Announce the winners: each place gets a shop voucher (≥ 100 EGP) and a notification. Once per group (409).</summary>
    [HttpPost("winners")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Winners(WinnersRequest request, CancellationToken ct) => Ok(await admin.WinnersAsync(request, ct), "Winners announced.");
}
