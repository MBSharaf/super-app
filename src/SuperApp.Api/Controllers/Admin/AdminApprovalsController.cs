using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Applications.Dtos;
using SuperApp.Application.Features.Applications.Services;

namespace SuperApp.Api.Controllers.Admin;

/// <summary>
/// Approvals queue for applications (API §7.2). Team accounts with a 2FA session; each application type needs its
/// permission (approvals.partners / .sellers / .factories) — checked in the service, other types are invisible.
/// </summary>
[Route("api/v1/admin/approvals")]
[Authorize(Policy = Policies.Staff)]
public sealed class AdminApprovalsController(IApprovalsService approvals) : ApiControllerBase
{
    /// <summary>?type=PitchOwner|Seller|Factory &amp;status=Pending|Approved|Rejected &amp;page &amp;pageSize — pending first, oldest first.</summary>
    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] ApprovalsQuery query, CancellationToken ct) => OkPage(await approvals.ListAsync(query, ct));

    [HttpGet("applications/{code}")]
    public async Task<ActionResult<object>> Get(string code, CancellationToken ct) => Ok(await approvals.GetAsync(code, ct));

    [HttpPost("applications/{code}/approve")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Approve(string code, CancellationToken ct) =>
        Ok(await approvals.ApproveAsync(code, ct), "Approved. The applicant was notified.");

    [HttpPost("applications/{code}/reject")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Reject(string code, RejectRequest request, CancellationToken ct) =>
        Ok(await approvals.RejectAsync(code, request, ct), "Rejected. The applicant was told why.");
}
