using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Bookings;

namespace SuperApp.Api.Controllers;

/// <summary>
/// Booking hours (API §3.3). Flow: GET availability → POST hold (10 minutes) → POST bookings. Until payments
/// arrive (Phase 5) a booking is confirmed straight away; "pay at the venue" only where the pitch allows cash.
/// </summary>
[Route("api/v1/bookings")]
[Authorize]
public sealed class BookingsController(IBookingsService bookings, IMyBookingsService mine) : ApiControllerBase
{
    [HttpPost("hold")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Hold(HoldRequest request, CancellationToken ct) =>
        Created(await bookings.HoldAsync(request, ct), "Held for 10 minutes.");

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Create(CreateBookingRequest request, CancellationToken ct) =>
        Created(await bookings.CreateAsync(request, ct), "Booking confirmed.");

    /// <summary>?tab=upcoming|past|cancelled &amp;page &amp;pageSize</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<object>> Mine([FromQuery] MyBookingsQuery query, CancellationToken ct) => OkPage(await mine.MineAsync(query, ct));

    [HttpGet("{code}")]
    public async Task<ActionResult<object>> Get(string code, CancellationToken ct) => Ok(await mine.GetAsync(code, ct));

    [HttpGet("{code}/refund-preview")]
    public async Task<ActionResult<object>> RefundPreview(string code, CancellationToken ct) => Ok(await mine.RefundPreviewAsync(code, ct));

    [HttpPost("{code}/cancel")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Cancel(string code, CancellationToken ct) =>
        Ok(await mine.CancelAsync(code, ct), "Booking cancelled.");

    /// <summary>The booking's QR as a PNG, to show at the gate.</summary>
    [HttpGet("{code}/qr")]
    public async Task<IActionResult> Qr(string code, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        return File(await mine.QrPngAsync(code, ct), "image/png");
    }
}
