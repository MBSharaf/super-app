using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Money;

namespace SuperApp.Api.Controllers;

/// <summary>Super Points, vouchers and code checks (API §5.3).</summary>
[Route("api/v1")]
[Authorize]
public sealed class PointsController(LoyaltyService loyalty) : ApiControllerBase
{
    [HttpGet("points")]
    public async Task<ActionResult<object>> Points(CancellationToken ct) => Ok(await loyalty.PointsAsync(ct));

    /// <summary>{ amount } in hundreds of EGP → a voucher. 422 not_multiple / under_minimum / not_enough_points.</summary>
    [HttpPost("points/redeem")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Redeem(RedeemRequest request, CancellationToken ct) => Created(await loyalty.RedeemAsync(request, ct), "Voucher ready.");

    [HttpGet("vouchers/mine")]
    public async Task<ActionResult<object>> Vouchers(CancellationToken ct) => Ok(await loyalty.VouchersAsync(ct));

    /// <summary>{ code, pitchSlug, total } → valid, discount, reason (a bad code is 200 with valid = false).</summary>
    [HttpPost("codes/check")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Check(CodeCheckRequest request, CancellationToken ct) => Ok(await loyalty.CheckAsync(request, ct));
}

/// <summary>The venue's own booking codes (owner or manager).</summary>
[Route("api/v1/partner/codes")]
[Authorize]
public sealed class PartnerCodesController(PartnerCodesService codes) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> List(CancellationToken ct) => Ok(await codes.ListAsync(ct));

    /// <summary>409 code_exists · 422 not_your_pitch</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Create(PartnerCodeRequest request, CancellationToken ct) => Created(await codes.CreateAsync(request, ct), "Code created.");

    [HttpPut("{id:long}")]
    public async Task<ActionResult<object>> Update(long id, PartnerCodeRequest request, CancellationToken ct) => Ok(await codes.UpdateAsync(id, request, ct));

    [HttpPost("{id:long}/pause")]
    public async Task<ActionResult<object>> Pause(long id, CancellationToken ct) => Ok(await codes.PauseAsync(id, ct));

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<object>> Delete(long id, CancellationToken ct)
    {
        await codes.DeleteAsync(id, ct);
        return Ok(new { id }, "Code deleted.");
    }
}
