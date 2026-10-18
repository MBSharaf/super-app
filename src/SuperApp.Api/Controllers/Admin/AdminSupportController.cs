using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Support;

namespace SuperApp.Api.Controllers.Admin;

/// <summary>The support inbox (2FA): read (support.read), reply / internal notes (support.reply), assign and priority (support.assign), disputes (disputes.decide).</summary>
[Route("api/v1/admin/support/tickets")]
[RequirePermission("support.read")]
public sealed class AdminSupportController(ISupportAdminService support) : ApiControllerBase
{
    /// <summary>?status (default: open ones) &amp;priority &amp;assignee — urgent first, then oldest.</summary>
    [HttpGet]
    public async Task<ActionResult<object>> Inbox([FromQuery] TicketsQuery query, CancellationToken ct) => OkPage(await support.InboxAsync(query, ct));

    /// <summary>The ticket with every message (internal notes too); the customer's messages are marked read.</summary>
    [HttpGet("{code}")]
    public async Task<ActionResult<object>> Get(string code, CancellationToken ct) => Ok(await support.GetAsync(code, ct));

    [HttpPost("{code}/messages")]
    [RequirePermission("support.reply")]
    [EnableRateLimiting(RateLimitPolicies.Chat)]
    public async Task<ActionResult<object>> Reply(string code, TeamMessageRequest request, CancellationToken ct) => Created(await support.ReplyAsync(code, request, ct));

    [HttpPost("{code}/assign")]
    [RequirePermission("support.assign")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Assign(string code, AssignRequest request, CancellationToken ct) => Ok(await support.AssignAsync(code, request, ct), "Assigned.");

    [HttpPost("{code}/priority")]
    [RequirePermission("support.assign")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Priority(string code, PriorityRequest request, CancellationToken ct) => Ok(await support.PriorityAsync(code, request, ct), "Priority saved.");

    [HttpPost("{code}/resolve")]
    [RequirePermission("support.reply")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Resolve(string code, CancellationToken ct) => Ok(await support.ResolveAsync(code, ct), "Resolved.");

    /// <summary>{ refund, paidBy: SuperApp | Partner | Seller, note } — recorded and both sides told; the refund is paid in Phase 5.</summary>
    [HttpPost("{code}/dispute")]
    [RequirePermission("disputes.decide")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Dispute(string code, DisputeRequest request, CancellationToken ct) => Ok(await support.DisputeAsync(code, request, ct), "Dispute decided.");
}
