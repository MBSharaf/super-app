using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Waitlist;

namespace SuperApp.Api.Controllers;

/// <summary>
/// The waitlist (API §3.7): wait for a booked hour; everyone waiting is told the moment it is freed. A free hour → 422
/// available (book it instead); already waiting → 409 already_waiting.
/// </summary>
[Route("api/v1/waitlist")]
[Authorize]
public sealed class WaitlistController(IWaitlistService waitlist) : ApiControllerBase
{
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Join(JoinWaitlistRequest request, CancellationToken ct) =>
        Created(await waitlist.JoinAsync(request, ct), "You’re on the waitlist — we’ll notify you.");

    [HttpGet("mine")]
    public async Task<ActionResult<object>> Mine(CancellationToken ct) => Ok(await waitlist.MineAsync(ct));

    [HttpDelete("{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Leave(long id, CancellationToken ct)
    {
        await waitlist.LeaveAsync(id, ct);
        return Ok<object?>(null, "Removed from the waitlist.");
    }
}
