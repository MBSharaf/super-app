using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Webhooks;

namespace SuperApp.Api.Controllers.Ceo;

/// <summary>
/// Webhooks for n8n and other outside systems (CEO only, with 2FA): events booking.created, booking.cancelled,
/// application.submitted, pitch.submitted, review.created, support.ticket.created, checkin.accepted — or "*" for all.
/// </summary>
[Route("api/v1/ceo/webhooks")]
[Authorize(Policy = Policies.Ceo)]
public sealed class CeoWebhooksController(IWebhooksService webhooks) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> List(CancellationToken ct) => Ok(await webhooks.ListAsync(ct));

    /// <summary>The events that can be followed.</summary>
    [HttpGet("events")]
    public ActionResult<object> Events() => Ok(WebhookEvents.All);

    /// <summary>The answer holds the signing secret — shown only this once.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Create(WebhookRequest request, CancellationToken ct) => Created(await webhooks.CreateAsync(request, ct), "Webhook added. Copy the secret now.");

    [HttpPut("{id:int}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Update(int id, WebhookUpdateRequest request, CancellationToken ct) => Ok(await webhooks.UpdateAsync(id, request, ct), "Saved.");

    [HttpDelete("{id:int}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Delete(int id, CancellationToken ct)
    {
        await webhooks.DeleteAsync(id, ct);
        return Ok<object?>(null, "Webhook deleted.");
    }

    /// <summary>Sends a signed "webhook.test" event now and returns what the receiver answered.</summary>
    [HttpPost("{id:int}/test")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Test(int id, CancellationToken ct) => Ok(await webhooks.TestAsync(id, ct));

    [HttpGet("{id:int}/deliveries")]
    public async Task<ActionResult<object>> Deliveries(int id, CancellationToken ct) => Ok(await webhooks.DeliveriesAsync(id, ct));
}
