using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Packages;

namespace SuperApp.Api.Controllers.Admin;

/// <summary>Package discounts waiting for approval (permission approvals.packages + 2FA). Approve as asked or lower; reject with a reason.</summary>
[Route("api/v1/admin/package-options")]
[RequirePermission("approvals.packages")]
public sealed class AdminPackageOptionsController(IPackageOptionsService options) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> Waiting([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        OkPage(await options.WaitingAsync(page, pageSize, ct));

    [HttpPost("{id:long}/approve")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Approve(long id, ApproveOptionRequest request, CancellationToken ct)
    {
        await options.ApproveAsync(id, request, ct);
        return Ok<object?>(null, "Package approved.");
    }

    [HttpPost("{id:long}/reject")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Reject(long id, RejectOptionRequest request, CancellationToken ct)
    {
        await options.RejectAsync(id, request, ct);
        return Ok<object?>(null, "Sent back to the partner.");
    }
}
