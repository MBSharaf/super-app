using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Partners;

namespace SuperApp.Api.Controllers.Partner;

/// <summary>
/// The partner dashboard's venues, pitches and blocked times (API §3.2). The owner and managers change them; check-in
/// staff and accountants can read. Another partner's ids answer 404.
/// </summary>
[Route("api/v1/partner")]
[Authorize]
public sealed class PartnerPitchesController(
    IPartnerVenuesService venues,
    IPartnerPitchesService pitches,
    IPartnerClosuresService closures) : ApiControllerBase
{
    [HttpGet("venues")]
    public async Task<ActionResult<object>> Venues(CancellationToken ct) => Ok(await venues.ListAsync(ct));

    [HttpPost("venues")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> AddVenue(VenueRequest request, CancellationToken ct) => Created(await venues.CreateAsync(request, ct), "Venue added.");

    [HttpPut("venues/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> EditVenue(long id, VenueRequest request, CancellationToken ct) => Ok(await venues.UpdateAsync(id, request, ct), "Venue saved.");

    [HttpGet("pitches")]
    public async Task<ActionResult<object>> Pitches(CancellationToken ct) => Ok(await pitches.ListAsync(ct));

    [HttpGet("pitches/{id:long}")]
    public async Task<ActionResult<object>> Pitch(long id, CancellationToken ct) => Ok(await pitches.GetAsync(id, ct));

    /// <summary>A new pitch waits for the admin's review.</summary>
    [HttpPost("pitches")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> AddPitch(PitchRequest request, CancellationToken ct) =>
        Created(await pitches.CreateAsync(request, ct), "Sent for review. We’ll notify you.");

    /// <summary>Send the rowVersion from the GET; someone else's newer change → 409 stale.</summary>
    [HttpPut("pitches/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> EditPitch(long id, PitchRequest request, CancellationToken ct) => Ok(await pitches.UpdateAsync(id, request, ct), "Pitch saved.");

    [HttpPost("pitches/{id:long}/close")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Close(long id, CancellationToken ct) => Ok(await pitches.CloseAsync(id, ct), "Closed for new bookings.");

    [HttpPost("pitches/{id:long}/reopen")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Reopen(long id, CancellationToken ct) => Ok(await pitches.ReopenAsync(id, ct), "Open for booking again.");

    [HttpGet("pitches/{id:long}/closures")]
    public async Task<ActionResult<object>> Closures(long id, CancellationToken ct) => Ok(await closures.ListAsync(id, ct));

    /// <summary>Booked hours in the range → 409 bookings_in_range (cancel them first).</summary>
    [HttpPost("pitches/{id:long}/closures")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> AddClosure(long id, ClosureRequest request, CancellationToken ct) =>
        Created(await closures.CreateAsync(id, request, ct), "Time blocked.");

    [HttpDelete("pitches/{id:long}/closures/{closureId:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> RemoveClosure(long id, long closureId, CancellationToken ct)
    {
        await closures.DeleteAsync(id, closureId, ct);
        return Ok<object?>(null, "Time unblocked.");
    }
}
