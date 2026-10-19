using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.FreeSlots;

namespace SuperApp.Api.Controllers;

/// <summary>Free slots (API §3.7): public cards, partners post and remove; players follow pitches or areas (alerts).</summary>
[Route("api/v1")]
[Authorize]
public sealed class FreeSlotsController(IFreeSlotsService freeSlots) : ApiControllerBase
{
    /// <summary>?areaId &amp;from &amp;to — live free hours that are still free.</summary>
    [HttpGet("free-slots")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> Live([FromQuery] FreeSlotsQuery query, CancellationToken ct) => Ok(await freeSlots.LiveAsync(query, ct));

    [HttpGet("partner/free-slots")]
    public async Task<ActionResult<object>> Mine(CancellationToken ct) => Ok(await freeSlots.PartnerListAsync(ct));

    /// <summary>Daily limit → 422 daily_limit; posting blocked → 423 posting_blocked; booked hour → 409.</summary>
    [HttpPost("partner/free-slots")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Post(PostFreeSlotRequest request, CancellationToken ct) => Created(await freeSlots.PostAsync(request, ct), "Free slot posted.");

    [HttpDelete("partner/free-slots/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Remove(long id, CancellationToken ct)
    {
        await freeSlots.RemoveAsync(id, ct);
        return Ok<object?>(null, "Free slot removed.");
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<object>> Alerts(CancellationToken ct) => Ok(await freeSlots.AlertsAsync(ct));

    [HttpPost("alerts")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Follow(AlertRequest request, CancellationToken ct) => Created(await freeSlots.FollowAsync(request, ct), "We’ll tell you about free slots.");

    [HttpDelete("alerts/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Unfollow(long id, CancellationToken ct)
    {
        await freeSlots.UnfollowAsync(id, ct);
        return Ok<object?>(null, "Alert removed.");
    }
}
