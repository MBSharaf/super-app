using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Shop.Shipping;

namespace SuperApp.Api.Controllers.Shop;

/// <summary>Order tracking, courier webhooks and admin parcel tools (API §4.7).</summary>
[Route("api/v1")]
public sealed class ShippingController(IShippingService shipping) : ApiControllerBase
{
    /// <summary>Every parcel of my order with its history.</summary>
    [HttpGet("orders/{code}/tracking")]
    [Authorize]
    public async Task<ActionResult<object>> Tracking(string code, CancellationToken ct) => Ok(await shipping.TrackingAsync(code, ct));

    /// <summary>Courier status updates, signed in X-Courier-Signature (HMAC-SHA256 of the body, hex). Replayed events are ignored.</summary>
    [HttpPost("webhooks/courier/{provider}")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(RateLimitPolicies.Agent)]
    public async Task<ActionResult<object>> Webhook(string provider, [FromHeader(Name = "X-Courier-Signature")] string? signature, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var applied = await shipping.WebhookAsync(provider, body, signature, ct);
        return Ok(new { applied }, applied ? "Updated." : "Already received.");
    }

    /// <summary>?status &amp;provider &amp;page &amp;pageSize</summary>
    [HttpGet("admin/shipments")]
    [RequirePermission("shipping.read")]
    public async Task<ActionResult<object>> List([FromQuery] string? status, [FromQuery] string? provider, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        OkPage(await shipping.ListAsync(status, provider, page, pageSize, ct));

    [HttpPost("admin/shipments/{id:long}/cancel")]
    [RequirePermission("shipping.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Cancel(long id, CancellationToken ct) => Ok(await shipping.CancelAsync(id, ct), "Parcel cancelled.");

    [HttpPost("admin/shipments/{id:long}/rebook")]
    [RequirePermission("shipping.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Rebook(long id, CancellationToken ct) => Ok(await shipping.RebookAsync(id, ct), "Parcel booked again.");

    /// <summary>Testing only: moves a simulated parcel to a status (403 not_simulated for real couriers).</summary>
    [HttpPost("admin/shipments/{id:long}/simulate")]
    [RequirePermission("shipping.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Simulate(long id, SimulateRequest request, CancellationToken ct) => Ok(await shipping.SimulateAsync(id, request, ct));
}
