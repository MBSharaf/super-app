using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Features.Shop.Stores;

namespace SuperApp.Api.Controllers.Shop;

/// <summary>
/// The same store tools for every kind of store (API §4.4 own products, §4.5 partner store, §7.2 official store): profile,
/// products with photos and stock, and the store's parts of orders (pack → ship with the courier → deliver, or cancel).
/// Each subclass only says which store the caller runs.
/// </summary>
public abstract class StoreOwnerControllerBase(IStoreProfileService profile, IStoreProductsService products, IStoreOrdersService orders) : ApiControllerBase
{
    private const long MaxUpload = 40 * 1024 * 1024;

    protected abstract Task<StoreScope> ScopeAsync(bool products, CancellationToken ct);

    [HttpGet("store")]
    public async Task<ActionResult<object>> Profile(CancellationToken ct) => Ok(await profile.GetAsync(await ScopeAsync(false, ct), ct));

    /// <summary>Handle taken → 409 handle_taken. A break until a date hides the store's items.</summary>
    [HttpPut("store")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> SaveProfile(StoreProfileRequest request, CancellationToken ct) =>
        Ok(await profile.UpdateAsync(await ScopeAsync(false, ct), request, ct), "Store saved.");

    /// <summary>multipart "file" (JPEG / PNG / WEBP, 5 MB).</summary>
    [HttpPost("store/{kind:regex(^(logo|cover)$)}")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(MaxUpload)]
    public async Task<ActionResult<object>> StoreImage(string kind, IFormFile file, CancellationToken ct) =>
        Ok(await profile.ImageAsync(await ScopeAsync(false, ct), kind, Upload(file), ct), "Saved.");

    [HttpGet("store/products")]
    public async Task<ActionResult<object>> Products(CancellationToken ct) => Ok(await products.ListAsync(await ScopeAsync(true, ct), ct));

    [HttpGet("store/products/{id:long}")]
    public async Task<ActionResult<object>> Product(long id, CancellationToken ct) => Ok(await products.GetAsync(await ScopeAsync(true, ct), id, ct));

    [HttpPost("store/products")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> AddProduct(ProductRequest request, CancellationToken ct) =>
        Created(await products.CreateAsync(await ScopeAsync(true, ct), request, ct), "Product added.");

    [HttpPut("store/products/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> EditProduct(long id, ProductRequest request, CancellationToken ct) =>
        Ok(await products.UpdateAsync(await ScopeAsync(true, ct), id, request, ct), "Product saved.");

    [HttpDelete("store/products/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> DeleteProduct(long id, CancellationToken ct)
    {
        await products.DeleteAsync(await ScopeAsync(true, ct), id, ct);
        return Ok<object?>(null, "Product deleted.");
    }

    [HttpPost("store/products/{id:long}/restock")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Restock(long id, RestockRequest request, CancellationToken ct) =>
        Ok(await products.RestockAsync(await ScopeAsync(true, ct), id, request, ct), "Stock added.");

    /// <summary>multipart "photos" (JPEG / PNG / WEBP, 5 MB each); up to 8 per product.</summary>
    [HttpPost("store/products/{id:long}/images")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(MaxUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUpload)]
    public async Task<ActionResult<object>> AddImages(long id, [FromForm(Name = "photos")] List<IFormFile> files, CancellationToken ct) =>
        Created(await products.AddImagesAsync(await ScopeAsync(true, ct), id, files.Select(Upload).ToList(), ct), "Photos added.");

    [HttpDelete("store/products/{id:long}/images/{imageId:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> DeleteImage(long id, long imageId, CancellationToken ct) =>
        Ok(await products.DeleteImageAsync(await ScopeAsync(true, ct), id, imageId, ct), "Photo deleted.");

    /// <summary>Every photo id once; the first is the cover.</summary>
    [HttpPut("store/products/{id:long}/images/order")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> OrderImages(long id, ImageOrderRequest request, CancellationToken ct) =>
        Ok(await products.OrderImagesAsync(await ScopeAsync(true, ct), id, request, ct), "Order saved.");

    /// <summary>?status=open|new|packed|shipped|delivered|cancelled|all &amp;q &amp;page &amp;pageSize</summary>
    [HttpGet("store/orders")]
    public async Task<ActionResult<object>> Orders([FromQuery] StoreOrdersQuery query, CancellationToken ct) => OkPage(await orders.ListAsync(await ScopeAsync(false, ct), query, ct));

    [HttpGet("store/orders/{code}")]
    public async Task<ActionResult<object>> Order(string code, CancellationToken ct) => Ok(await orders.GetAsync(await ScopeAsync(false, ct), code, ct));

    [HttpPost("store/orders/{code}/pack")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Pack(string code, CancellationToken ct) => Ok(await orders.PackAsync(await ScopeAsync(false, ct), code, ct), "Packed.");

    /// <summary>Books the courier; the customer gets the tracking number.</summary>
    [HttpPost("store/orders/{code}/ship")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Ship(string code, CancellationToken ct) => Ok(await orders.ShipAsync(await ScopeAsync(false, ct), code, ct), "Courier booked.");

    [HttpPost("store/orders/{code}/deliver")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Deliver(string code, CancellationToken ct) => Ok(await orders.DeliverAsync(await ScopeAsync(false, ct), code, ct), "Delivered.");

    [HttpPost("store/orders/{code}/cancel")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Cancel(string code, CancelReasonRequest request, CancellationToken ct) =>
        Ok(await orders.CancelAsync(await ScopeAsync(false, ct), code, request, ct), "Cancelled. The customer was told.");

    private static UploadedFile Upload(IFormFile f) => new(f.OpenReadStream(), f.FileName, f.ContentType, f.Length);
}

/// <summary>A pitch partner's shop (API §4.5): the owner or a manager; made on first use.</summary>
[Route("api/v1/partner")]
[Authorize]
public sealed class PartnerStoreController(IStoreScopes scopes, IStoreProfileService profile, IStoreProductsService products, IStoreOrdersService orders)
    : StoreOwnerControllerBase(profile, products, orders)
{
    protected override Task<StoreScope> ScopeAsync(bool products, CancellationToken ct) => scopes.PartnerAsync(ct);
}

/// <summary>A seller's store and own products (API §4.4). Own products need the "store" or "both" mode (422 own_products_off).</summary>
[Route("api/v1/seller")]
[Authorize]
public sealed class SellerStoreController(IStoreScopes scopes, IStoreProfileService profile, IStoreProductsService products, IStoreOrdersService orders)
    : StoreOwnerControllerBase(profile, products, orders)
{
    protected override Task<StoreScope> ScopeAsync(bool products, CancellationToken ct) => scopes.SellerAsync(products, ct);
}

/// <summary>The Super App Store (API §7.2 "Official store"): staff with store.official + 2FA.</summary>
[Route("api/v1/admin")]
[RequirePermission("store.official")]
public sealed class AdminStoreController(IStoreScopes scopes, IStoreProfileService profile, IStoreProductsService products, IStoreOrdersService orders)
    : StoreOwnerControllerBase(profile, products, orders)
{
    protected override Task<StoreScope> ScopeAsync(bool products, CancellationToken ct) => scopes.OfficialAsync(ct);
}

/// <summary>Hide one product of any store with a reason, or show it again (FR-SHOP-10): partners.manage + 2FA.</summary>
[Route("api/v1/admin/products")]
[RequirePermission("partners.manage")]
public sealed class AdminProductsController(IAdminProductsService products) : ApiControllerBase
{
    [HttpPost("{id:long}/hide")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Hide(long id, HideProductRequest request, CancellationToken ct)
    {
        await products.HideAsync(id, request, ct);
        return Ok<object?>(null, "Hidden. The store was told.");
    }

    [HttpPost("{id:long}/unhide")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Unhide(long id, CancellationToken ct)
    {
        await products.UnhideAsync(id, ct);
        return Ok<object?>(null, "Back in the shop.");
    }
}
