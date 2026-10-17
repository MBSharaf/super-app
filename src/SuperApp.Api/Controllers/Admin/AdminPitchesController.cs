using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Partners;

namespace SuperApp.Api.Controllers.Admin;

/// <summary>Review of new pitches (API §7.2): permission approvals.pitches with a 2FA session. Approve → live; reject with a reason.</summary>
[Route("api/v1/admin/pitches")]
[RequirePermission("approvals.pitches")]
public sealed class AdminPitchesController(IPitchReviewsService reviews) : ApiControllerBase
{
    /// <summary>?status=WaitingForReview (default)|Live|Rejected|ClosedByOwner|ClosedByAdmin &amp;page &amp;pageSize — oldest first.</summary>
    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] PitchReviewsQuery query, CancellationToken ct) => OkPage(await reviews.ListAsync(query, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<object>> Get(long id, CancellationToken ct) => Ok(await reviews.GetAsync(id, ct));

    [HttpPost("{id:long}/approve")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Approve(long id, CancellationToken ct) => Ok(await reviews.ApproveAsync(id, ct), "Pitch approved. It is live now.");

    [HttpPost("{id:long}/reject")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Reject(long id, PitchRejectRequest request, CancellationToken ct) =>
        Ok(await reviews.RejectAsync(id, request, ct), "Sent back to the partner with your reason.");
}
