using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Shop.Orders;

namespace SuperApp.Api.Controllers.Shop;

/// <summary>
/// Checkout and "My orders" (API §4.2). Checkout needs an <c>Idempotency-Key</c> header: a retry with the same key gets the
/// same order. Until Phase 5 online and wallet payments are simulated (the order is paid at once).
/// </summary>
[Route("api/v1/orders")]
[Authorize]
public sealed class OrdersController(ICheckoutService checkout, IMyOrdersService orders) : ApiControllerBase
{
    /// <summary>409 item_unavailable / stock_changed / printing_paused · 422 empty_cart / prepay_required / code errors · 423 shop paused.</summary>
    [HttpPost("checkout")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Checkout(CheckoutRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct) =>
        Created(await checkout.CheckoutAsync(request, idempotencyKey ?? string.Empty, ct), "Order placed.");

    /// <summary>?status=open|completed|cancelled|all &amp;page &amp;pageSize</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<object>> Mine([FromQuery] MyOrdersQuery query, CancellationToken ct) => OkPage(await orders.MineAsync(query, ct));

    [HttpGet("{code}")]
    public async Task<ActionResult<object>> Get(string code, CancellationToken ct) => Ok(await orders.GetAsync(code, ct));

    /// <summary>Before anything is packed, printing or shipped; else 422 too_late.</summary>
    [HttpPost("{code}/cancel")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Cancel(string code, CancellationToken ct) => Ok(await orders.CancelAsync(code, ct), "Order cancelled.");

    [HttpGet("{code}/receipt.pdf")]
    public async Task<IActionResult> Receipt(string code, CancellationToken ct)
    {
        var (file, name) = await orders.ReceiptAsync(code, ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(file, "application/pdf", name);
    }
}
