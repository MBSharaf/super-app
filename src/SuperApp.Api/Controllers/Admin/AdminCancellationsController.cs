using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Cancellations;

namespace SuperApp.Api.Controllers.Admin;

/// <summary>
/// The cancellation queue (support.read; deciding: support.reply). The AI agent works the same queue with its API key
/// (its n8n trigger is the cancellation.requested webhook). Approve cancels and refunds under the usual rules.
/// </summary>
[Route("api/v1/admin/cancellations")]
[RequirePermission("support.read")]
public sealed class AdminCancellationsController(ICancellationsAdminService cancellations) : ApiControllerBase
{
    /// <summary>?status = pending (default, oldest due first) | answered | all.</summary>
    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] CancellationsQuery query, CancellationToken ct) => OkPage(await cancellations.ListAsync(query, ct));

    [HttpGet("{code}")]
    public async Task<ActionResult<object>> Get(string code, CancellationToken ct) => Ok(await cancellations.GetAsync(code, ct));

    /// <summary>{ note? } — the item is cancelled and refunded; the customer is told.</summary>
    [HttpPost("{code}/approve")]
    [RequirePermission("support.reply")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Approve(string code, CancellationDecisionBody body, CancellationToken ct) =>
        Ok(await cancellations.ApproveAsync(code, body, ct), "Approved — cancelled and refunded.");

    /// <summary>{ note } — the reason the customer sees (5+ characters); the item stays active.</summary>
    [HttpPost("{code}/reject")]
    [RequirePermission("support.reply")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Reject(string code, CancellationDecisionBody body, CancellationToken ct) =>
        Ok(await cancellations.RejectAsync(code, body, ct), "Refused — the customer is told.");
}
