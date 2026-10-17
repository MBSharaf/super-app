using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Shop.Catalog;

namespace SuperApp.Api.Controllers.Shop;

/// <summary>
/// The public shop (API §4.1): one list of store products and live printed designs, the product page, the printed-design
/// page with its art, and design reports. Hidden, unpublished or not-live items answer 404.
/// </summary>
[Route("api/v1/shop")]
[AllowAnonymous]
public sealed class ShopController(IShopService shop) : ApiControllerBase
{
    /// <summary>?q &amp;category &amp;designCategory &amp;store &amp;tag &amp;minPrice &amp;maxPrice &amp;kind=product|design &amp;sort=featured|price|-price|rating|newest &amp;page &amp;pageSize</summary>
    [HttpGet("products")]
    public async Task<ActionResult<object>> List([FromQuery] ShopQuery query, CancellationToken ct) => OkPage(await shop.ListAsync(query, ct));

    [HttpGet("products/{slug}")]
    public async Task<ActionResult<object>> Product(string slug, CancellationToken ct) => Ok(await shop.ProductAsync(slug, ct));

    [HttpGet("designs/{id:long}")]
    public async Task<ActionResult<object>> Design(long id, CancellationToken ct) => Ok(await shop.DesignAsync(id, ct));

    /// <summary>The art of one side as a resized PNG (or the SVG). Not live: only its seller and the reviewers.</summary>
    [HttpGet("designs/{id:long}/art/{side:regex(^(front|back)$)}")]
    public async Task<IActionResult> Art(long id, string side, CancellationToken ct)
    {
        var art = await shop.ArtAsync(id, side, ct);
        Response.Headers.CacheControl = "private, max-age=300";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";
        return File(art.Bytes, art.ContentType);
    }

    /// <summary>Copyright, trademark, offensive or other. One open report per person and design (409 already_reported).</summary>
    [HttpPost("designs/{id:long}/report")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Report(long id, ReportDesignRequest request, CancellationToken ct)
    {
        await shop.ReportAsync(id, request, ct);
        return Created<object?>(null, "Thanks — our team will review this design.");
    }
}
