using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Filters;
using SuperApp.Api.Security;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Shop.Pod;

namespace SuperApp.Api.Controllers.Shop;

/// <summary>
/// The seller dashboard and design studio (API §4.4). The store profile, own products and own-product orders are in
/// <see cref="SellerStoreController"/> under the same <c>/seller</c> address.
/// </summary>
[Route("api/v1/seller")]
[Authorize]
public sealed class SellerController(
    ISellerDesignsService designs,
    IBulkDesignsService bulk,
    ISellerInsightsService insights,
    ISamplesService samples,
    ISellerCodesService codes,
    ISellerKycService kyc) : ApiControllerBase
{
    private const long MaxArt = 9 * 1024 * 1024;
    private const long MaxBulk = 120 * 1024 * 1024;
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [HttpGet("overview")]
    public async Task<ActionResult<object>> Overview(CancellationToken ct) => Ok(await insights.OverviewAsync(ct));

    /// <summary>Printable products with the base price for my level (the factory cost is never shown).</summary>
    [PrintOnDemand]
    [HttpGet("catalogue")]
    public async Task<ActionResult<object>> Catalogue(CancellationToken ct) => Ok(await designs.CatalogueAsync(ct));

    [PrintOnDemand]
    [HttpGet("designs")]
    public async Task<ActionResult<object>> Designs([FromQuery] string? status, CancellationToken ct) => Ok(await designs.ListAsync(status, ct));

    [PrintOnDemand]
    [HttpGet("designs/{id:long}")]
    public async Task<ActionResult<object>> Design(long id, CancellationToken ct) => Ok(await designs.GetAsync(id, ct));

    /// <summary>A new draft on a printable product. Level limit reached → 422 design_limit.</summary>
    [PrintOnDemand]
    [HttpPost("designs")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> CreateDesign(NewDesignRequest request, CancellationToken ct) => Created(await designs.CreateAsync(request, ct), "Draft created.");

    /// <summary>Send rowVersion from the last answer (409 stale). Price below the base / above the maximum → 422.</summary>
    [PrintOnDemand]
    [HttpPut("designs/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> SaveDesign(long id, DesignRequest request, CancellationToken ct) => Ok(await designs.UpdateAsync(id, request, ct), "Saved.");

    /// <summary>multipart "file": PNG, JPG, WEBP or SVG up to 8 MB. Answers the print quality (dpi).</summary>
    [PrintOnDemand]
    [HttpPost("designs/{id:long}/art/{side:regex(^(front|back)$)}")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(MaxArt)]
    public async Task<ActionResult<object>> UploadArt(long id, string side, IFormFile file, CancellationToken ct) =>
        Ok(await designs.UploadArtAsync(id, side, Upload(file), ct), "Artwork saved.");

    [PrintOnDemand]
    [HttpDelete("designs/{id:long}/art/{side:regex(^(front|back)$)}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> RemoveArt(long id, string side, CancellationToken ct) => Ok(await designs.RemoveArtAsync(id, side, ct), "Artwork removed.");

    /// <summary>Missing art, title, category or colours → 422 incomplete. Elite designs go live at once (spot check later).</summary>
    [PrintOnDemand]
    [HttpPost("designs/{id:long}/submit")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Submit(long id, CancellationToken ct) => Ok(await designs.SubmitAsync(id, ct), "Sent.");

    [PrintOnDemand]
    [HttpPost("designs/{id:long}/pause")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Pause(long id, CancellationToken ct) => Ok(await designs.PauseAsync(id, true, ct), "Paused.");

    [PrintOnDemand]
    [HttpPost("designs/{id:long}/resume")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Resume(long id, CancellationToken ct) => Ok(await designs.PauseAsync(id, false, ct), "Back on sale.");

    [PrintOnDemand]
    [HttpDelete("designs/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> DeleteDesign(long id, CancellationToken ct)
    {
        await designs.DeleteAsync(id, ct);
        return Ok<object?>(null, "Design deleted.");
    }

    /// <summary>multipart: "answers" (JSON BulkDesignRequest) + "files" (1–20 artworks). Pro and Elite (403 bulk_level).</summary>
    [PrintOnDemand]
    [HttpPost("designs/bulk")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(MaxBulk)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBulk)]
    public async Task<ActionResult<object>> Bulk([FromForm] string answers, [FromForm(Name = "files")] List<IFormFile> files, CancellationToken ct)
    {
        var request = Parse<BulkDesignRequest>(answers);
        return Created(await bulk.UploadAsync(request, files.Select(Upload).ToList(), ct), "Designs uploaded.");
    }

    /// <summary>Items sold (printed and own products) with the factory stage. ?status=open|delivered|cancelled|all</summary>
    [HttpGet("orders")]
    public async Task<ActionResult<object>> Orders([FromQuery] SellerOrdersQuery query, CancellationToken ct) => OkPage(await insights.OrdersAsync(query, ct));

    [PrintOnDemand]
    [HttpGet("samples")]
    public async Task<ActionResult<object>> Samples(CancellationToken ct) => Ok(await samples.ListAsync(ct));

    /// <summary>Paid from the available balance (422 balance_too_low); Elite gets one free a month.</summary>
    [PrintOnDemand]
    [HttpPost("samples")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> OrderSample(SampleRequest request, CancellationToken ct) => Created(await samples.OrderAsync(request, ct), "Sample ordered.");

    /// <summary>?from &amp;to (yyyy-MM-dd, up to a year; default the last 30 days).</summary>
    [HttpGet("analytics")]
    public async Task<ActionResult<object>> Analytics([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) => Ok(await insights.AnalyticsAsync(from, to, ct));

    /// <summary>Elite only (403 export_level). Dashboard sheet first.</summary>
    [HttpGet("analytics.xlsx")]
    public async Task<IActionResult> AnalyticsExcel([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var (file, name) = await insights.AnalyticsExcelAsync(from, to, ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    [HttpGet("calendar")]
    public async Task<ActionResult<object>> Calendar(CancellationToken ct) => Ok(await insights.CalendarAsync(ct));

    [HttpGet("kyc")]
    public async Task<ActionResult<object>> Kyc(CancellationToken ct) => Ok(await kyc.GetAsync(ct));

    /// <summary>multipart: "answers" ({ nationalId }) + "idFront" + "idBack".</summary>
    [HttpPost("kyc")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(MaxArt * 2)]
    public async Task<ActionResult<object>> SubmitKyc([FromForm] string answers, IFormFile idFront, IFormFile idBack, CancellationToken ct) =>
        Ok(await kyc.SubmitAsync(Parse<KycRequest>(answers), Upload(idFront), Upload(idBack), ct), "Sent for checking.");

    [HttpGet("codes")]
    public async Task<ActionResult<object>> Codes(CancellationToken ct) => Ok(await codes.ListAsync(ct));

    /// <summary>Taken → 409 code_taken; over the level's active codes → 422 code_limit.</summary>
    [HttpPost("codes")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> AddCode(SellerCodeRequest request, CancellationToken ct) => Created(await codes.CreateAsync(request, ct), "Code created.");

    [HttpPut("codes/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> EditCode(long id, SellerCodeRequest request, CancellationToken ct) => Ok(await codes.UpdateAsync(id, request, ct), "Code saved.");

    [HttpDelete("codes/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> DeleteCode(long id, CancellationToken ct)
    {
        await codes.DeleteAsync(id, ct);
        return Ok<object?>(null, "Code deleted.");
    }

    private static T Parse<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Web) ?? throw new BadRequestException("Send the settings as JSON in \"answers\".", "bad_answers");
        }
        catch (JsonException)
        {
            throw new BadRequestException("Send the settings as JSON in \"answers\".", "bad_answers");
        }
    }

    private static UploadedFile Upload(IFormFile f) => new(f.OpenReadStream(), f.FileName, f.ContentType, f.Length);
}

/// <summary>Design review and reports (API §7.2 "Designs"): designs.review / designs.reports + 2FA.</summary>
[Route("api/v1/admin")]
[PrintOnDemand]
public sealed class AdminDesignsController(IDesignReviewService review, IDesignReportsService reports) : ApiControllerBase
{
    /// <summary>Priority lane (Pro) first, then normal, then Elite spot checks.</summary>
    [HttpGet("designs/queue")]
    [RequirePermission("designs.review")]
    public async Task<ActionResult<object>> Queue([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => OkPage(await review.QueueAsync(page, pageSize, ct));

    [HttpPost("designs/{id:long}/approve")]
    [RequirePermission("designs.review")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Approve(long id, CancellationToken ct)
    {
        await review.ApproveAsync(id, ct);
        return Ok<object?>(null, "Approved. The seller was told.");
    }

    [HttpPost("designs/{id:long}/reject")]
    [RequirePermission("designs.review")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Reject(long id, RejectRequest request, CancellationToken ct)
    {
        await review.RejectAsync(id, request, ct);
        return Ok<object?>(null, "Sent back to the seller.");
    }

    [HttpGet("design-reports")]
    [RequirePermission("designs.reports")]
    public async Task<ActionResult<object>> Reports([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => OkPage(await reports.ListAsync(page, pageSize, ct));

    [HttpPost("design-reports/{id:long}/take-down")]
    [RequirePermission("designs.reports")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> TakeDown(long id, TakeDownRequest request, CancellationToken ct)
    {
        await reports.TakeDownAsync(id, request, ct);
        return Ok<object?>(null, "Design taken down.");
    }

    [HttpPost("design-reports/{id:long}/dismiss")]
    [RequirePermission("designs.reports")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Dismiss(long id, CancellationToken ct)
    {
        await reports.DismissAsync(id, ct);
        return Ok<object?>(null, "Report dismissed.");
    }
}
