using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Money;

namespace SuperApp.Api.Controllers;

/// <summary>Withdrawals for wallet owners (API §5.2): ask, list, statement.</summary>
[Route("api/v1/wallet")]
[Authorize]
public sealed class PayoutsController(PayoutsService payouts) : ApiControllerBase
{
    /// <summary>{ walletType (default PartnerBookings), amount } — 422 under_minimum / over_available / no_payout_method / account_not_active · 409 payout_open · 423 payouts paused.</summary>
    [HttpPost("payouts")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Ask(PayoutRequestBody body, CancellationToken ct) =>
        Created(await payouts.RequestAsync(body, ct), "Withdrawal requested.");

    [HttpGet("payouts")]
    public async Task<ActionResult<object>> Mine(CancellationToken ct) => Ok(await payouts.MineAsync(ct));

    [HttpGet("statement.xlsx")]
    public async Task<IActionResult> Statement([FromQuery] string? type, CancellationToken ct) =>
        File(await payouts.StatementAsync(type, ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "wallet-statement.xlsx");
}

/// <summary>Finance: prepare, approve, send or reject withdrawals (two different people; approval over the limit).</summary>
[Route("api/v1/admin")]
[Authorize]
public sealed class AdminPayoutsController(PayoutsService payouts) : ApiControllerBase
{
    /// <summary>?status=Requested|Prepared|WaitingForApproval|Sent|Rejected &amp;page &amp;pageSize</summary>
    [HttpGet("payouts")]
    [RequirePermission("payouts.read")]
    public async Task<ActionResult<object>> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        OkPage(await payouts.ListAsync(status, page, pageSize, ct));

    [HttpPost("payouts/{code}/prepare")]
    [RequirePermission("payouts.prepare")]
    public async Task<ActionResult<object>> Prepare(string code, CancellationToken ct) => Ok(await payouts.PrepareAsync(code, ct), "Prepared.");

    [HttpPost("payouts/{code}/send")]
    [RequirePermission("payouts.send")]
    public async Task<ActionResult<object>> Send(string code, PayoutSendBody body, CancellationToken ct) => Ok(await payouts.SendAsync(code, body, ct), "Sent.");

    [HttpPost("payouts/{code}/reject")]
    [RequirePermission("payouts.prepare")]
    public async Task<ActionResult<object>> Reject(string code, PayoutRejectBody body, CancellationToken ct) => Ok(await payouts.RejectAsync(code, body, ct), "Rejected.");

    [HttpGet("money-approvals")]
    [RequirePermission("finance.approve")]
    public async Task<ActionResult<object>> Approvals(CancellationToken ct) => Ok(await payouts.ApprovalsAsync(ct));

    /// <summary>{ decision: approve|reject, note } — 409 two_people for your own request.</summary>
    [HttpPost("money-approvals/{id:long}/decide")]
    [RequirePermission("finance.approve")]
    public async Task<ActionResult<object>> Decide(long id, ApprovalDecisionBody body, CancellationToken ct) => Ok(await payouts.DecideAsync(id, body, ct));
}
