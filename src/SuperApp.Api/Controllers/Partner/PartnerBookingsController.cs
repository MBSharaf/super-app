using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Partners;

namespace SuperApp.Api.Controllers.Partner;

/// <summary>
/// The venue's bookings (API §3.4). Everyone in the business can list and export; owner, manager and check-in staff
/// add walk-ins and mark no-shows; the owner or a manager cancels (the player gets a full refund).
/// </summary>
[Route("api/v1/partner/bookings")]
[Authorize]
public sealed class PartnerBookingsController(IPartnerBookingsService bookings) : ApiControllerBase
{
    /// <summary>?pitchId &amp;from &amp;to &amp;status=upcoming|past|cancelled|noshow|all &amp;q &amp;page &amp;pageSize</summary>
    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] PartnerBookingsQuery query, CancellationToken ct) => OkPage(await bookings.ListAsync(query, ct));

    /// <summary>The same filter as an Excel file (dashboard sheet first). Over 5,000 rows → 422 too_many_rows.</summary>
    [HttpGet("export.xlsx")]
    public async Task<IActionResult> Export([FromQuery] PartnerBookingsQuery query, CancellationToken ct)
    {
        var (file, name) = await bookings.ExportAsync(query, ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    [HttpPost("walk-in")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> WalkIn(WalkInRequest request, CancellationToken ct) => Created(await bookings.WalkInAsync(request, ct), "Walk-in booked.");

    [HttpPost("{code}/no-show")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> NoShow(string code, CancellationToken ct) => Ok(await bookings.NoShowAsync(code, ct), "Marked as a no-show.");

    [HttpPost("{code}/cancel")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Cancel(string code, VenueCancelRequest request, CancellationToken ct) =>
        Ok(await bookings.CancelAsync(code, request, ct), "Booking cancelled. The player was told.");
}
