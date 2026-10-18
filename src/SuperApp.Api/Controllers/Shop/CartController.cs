using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Shop.Orders;

namespace SuperApp.Api.Controllers.Shop;

/// <summary>
/// The cart and the wishlist (API §4.2). Every answer is the whole cart with live prices, stock, delivery fee and totals.
/// Promo codes are not stored: POST /cart/promo checks one, then send <c>?promo=CODE</c> with GET /cart and the code at checkout.
/// </summary>
[Route("api/v1")]
[Authorize]
public sealed class CartController(ICartService cart) : ApiControllerBase
{
    /// <summary>?promo=CODE &amp;speed=standard|express</summary>
    [HttpGet("cart")]
    public async Task<ActionResult<object>> Get([FromQuery] string? promo, [FromQuery] string? speed, CancellationToken ct) =>
        Ok(await cart.GetAsync(promo, speed == "express", ct));

    /// <summary>{ productVariantId } or { designId, size, color } and qty. Not enough stock → 409 out_of_stock; over 20 → 422 qty_limit.</summary>
    [HttpPost("cart/items")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Add(AddCartItemRequest request, CancellationToken ct) => Created(await cart.AddAsync(request, ct), "Added to your cart.");

    [HttpPatch("cart/items/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Update(long id, UpdateCartItemRequest request, CancellationToken ct) => Ok(await cart.UpdateAsync(id, request, ct));

    [HttpDelete("cart/items/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Remove(long id, CancellationToken ct) => Ok(await cart.RemoveAsync(id, ct), "Removed.");

    /// <summary>422 invalid_code, code_expired, code_used_up, min_spend or code_not_for_cart.</summary>
    [HttpPost("cart/promo")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> ApplyPromo(PromoRequest request, CancellationToken ct) => Ok(await cart.ApplyPromoAsync(request, ct), "Code applied.");

    /// <summary>The cart without a code (codes are not stored, so this only answers the cart again).</summary>
    [HttpDelete("cart/promo")]
    public async Task<ActionResult<object>> RemovePromo(CancellationToken ct) => Ok(await cart.GetAsync(null, false, ct), "Code removed.");

    [HttpGet("wishlist")]
    public async Task<ActionResult<object>> Wishlist(CancellationToken ct) => Ok(await cart.WishlistAsync(ct));

    /// <summary>{ productSlug } or { designId }; saving twice keeps one.</summary>
    [HttpPost("wishlist")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> AddWish(WishlistRequest request, CancellationToken ct) => Created(await cart.AddWishAsync(request, ct), "Saved.");

    [HttpDelete("wishlist/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> RemoveWish(long id, CancellationToken ct) => Ok(await cart.RemoveWishAsync(id, ct), "Removed.");
}
