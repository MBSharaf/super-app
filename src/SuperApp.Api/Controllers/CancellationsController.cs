using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Cancellations;

namespace SuperApp.Api.Controllers;

/// <summary>
/// Cancellation requests (owner's rule, 2026-11-07): a customer can't cancel a paid booking, package, order or game place
/// directly — "Cancel" posts here, the request gets its own number and the Super App team replies within 2 hours.
/// </summary>
[Route("api/v1/cancellations")]
[Authorize]
public sealed class CancellationsController(ICancellationsService cancellations) : ApiControllerBase
{
    /// <summary>{ kind: booking | package | order | game, reference, reason? } — 201; 409 already_requested; 422 no_payment / cannot_cancel / too_late.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Ask(CancellationRequestBody body, CancellationToken ct) =>
        Created(await cancellations.RequestAsync(body, ct), "Cancellation request sent. We’ll reply within 2 hours.");

    [HttpGet("mine")]
    public async Task<ActionResult<object>> Mine(CancellationToken ct) => Ok(await cancellations.MineAsync(ct));
}
