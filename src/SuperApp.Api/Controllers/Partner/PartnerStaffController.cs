using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Partners;

namespace SuperApp.Api.Controllers.Partner;

/// <summary>
/// The partner's staff accounts (API §3.2): managers, check-in staff and accountants. The owner and managers manage
/// them; only the owner handles managers. The person needs a Super App account first (found by email or mobile).
/// </summary>
[Route("api/v1/partner/staff")]
[Authorize]
public sealed class PartnerStaffController(IPartnerStaffService staff) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> List(CancellationToken ct) => Ok(await staff.ListAsync(ct));

    /// <summary>No account with this login → 404 no_account; already works for a partner → 409 already_staff.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Add(AddStaffRequest request, CancellationToken ct) => Created(await staff.AddAsync(request, ct), "Staff member added.");

    [HttpPut("{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Update(long id, UpdateStaffRequest request, CancellationToken ct) => Ok(await staff.UpdateAsync(id, request, ct), "Saved.");

    [HttpDelete("{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Remove(long id, CancellationToken ct)
    {
        await staff.RemoveAsync(id, ct);
        return Ok<object?>(null, "Staff member removed.");
    }
}
