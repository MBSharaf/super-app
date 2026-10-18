using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Reviews;

namespace SuperApp.Api.Controllers.Admin;

/// <summary>Reported reviews (permission reviews.moderate + 2FA): hide the review (it leaves the rating) or keep it.</summary>
[Route("api/v1/admin/review-reports")]
[RequirePermission("reviews.moderate")]
public sealed class AdminReviewReportsController(IPartnerReviewsService reviews) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> Open([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        OkPage(await reviews.OpenReportsAsync(page, pageSize, ct));

    [HttpPost("{id:long}/hide")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Hide(long id, CancellationToken ct)
    {
        await reviews.DecideAsync(id, hide: true, ct);
        return Ok<object?>(null, "Review hidden.");
    }

    [HttpPost("{id:long}/keep")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Keep(long id, CancellationToken ct)
    {
        await reviews.DecideAsync(id, hide: false, ct);
        return Ok<object?>(null, "Review kept.");
    }
}
