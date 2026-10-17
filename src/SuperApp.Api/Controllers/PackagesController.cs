using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Packages;

namespace SuperApp.Api.Controllers;

/// <summary>
/// Weekly packages (API §3.5): the same hour every week for 1–12 matches, with the pitch's approved discount and one QR
/// for every match. The quote is public; buying needs a signed-in account.
/// </summary>
[Route("api/v1/packages")]
[Authorize]
public sealed class PackagesController(IPackagesService packages) : ApiControllerBase
{
    /// <summary>?pitchSlug &amp;optionId &amp;startsOn=yyyy-MM-dd &amp;hour &amp;duration — not offered → 422 size_not_offered; too few free weeks → 409 not_enough_weeks.</summary>
    [HttpGet("quote")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> Quote([FromQuery] PackageQuoteQuery query, CancellationToken ct) => Ok(await packages.QuoteAsync(query, ct));

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Buy(BuyPackageRequest request, CancellationToken ct) => Created(await packages.BuyAsync(request, ct), "Package booked.");

    [HttpGet("mine")]
    public async Task<ActionResult<object>> Mine(CancellationToken ct) => Ok(await packages.MineAsync(ct));

    [HttpGet("{code}")]
    public async Task<ActionResult<object>> Get(string code, CancellationToken ct) => Ok(await packages.GetAsync(code, ct));

    /// <summary>The one QR for every match, as a PNG.</summary>
    [HttpGet("{code}/qr")]
    public async Task<IActionResult> Qr(string code, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        return File(await packages.QrPngAsync(code, ct), "image/png");
    }
}
