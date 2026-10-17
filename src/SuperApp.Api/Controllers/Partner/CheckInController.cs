using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.CheckIn;

namespace SuperApp.Api.Controllers.Partner;

/// <summary>
/// QR check-in at the venue (API §3.6): owner, managers and check-in staff. A rejected scan answers 200 with
/// result "rejected" and the reason; send a new Idempotency-Key with each scan (the same key repeats the answer).
/// </summary>
[Route("api/v1/check-in")]
[Authorize]
public sealed class CheckInController(ICheckInService checkIn) : ApiControllerBase
{
    [HttpPost("scan")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Scan(ScanRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        var result = await checkIn.ScanAsync(request, idempotencyKey ?? string.Empty, ct);
        return Ok(result, result.Message);
    }

    /// <summary>?pitchId &amp;date=yyyy-MM-dd (default today, Cairo)</summary>
    [HttpGet("history")]
    public async Task<ActionResult<object>> History([FromQuery] CheckInHistoryQuery query, CancellationToken ct) => Ok(await checkIn.HistoryAsync(query, ct));
}
