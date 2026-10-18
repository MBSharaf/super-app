using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Reviews;

namespace SuperApp.Api.Controllers.Partner;

/// <summary>Reviews of the partner's pitches (API §3.10): everyone in the business reads; the owner or a manager replies and reports.</summary>
[Route("api/v1/partner/reviews")]
[Authorize]
public sealed class PartnerReviewsController(IPartnerReviewsService reviews) : ApiControllerBase
{
    /// <summary>?pitchId &amp;page &amp;pageSize — newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] PartnerReviewsQuery query, CancellationToken ct) => OkPage(await reviews.ListAsync(query, ct));

    [HttpPost("{id:long}/reply")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Reply(long id, ReplyRequest request, CancellationToken ct)
    {
        await reviews.ReplyAsync(id, request, ct);
        return Ok<object?>(null, "Reply posted.");
    }

    [HttpDelete("{id:long}/reply")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> RemoveReply(long id, CancellationToken ct)
    {
        await reviews.RemoveReplyAsync(id, ct);
        return Ok<object?>(null, "Reply removed.");
    }

    /// <summary>Already reported and still open → 409 already_reported.</summary>
    [HttpPost("{id:long}/report")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Report(long id, ReportRequest request, CancellationToken ct)
    {
        await reviews.ReportAsync(id, request, ct);
        return Ok<object?>(null, "Reported. Super App will check it.");
    }
}
