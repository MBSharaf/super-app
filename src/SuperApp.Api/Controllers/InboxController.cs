using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Inbox;

namespace SuperApp.Api.Controllers;

/// <summary>The signed-in person's bell and which kinds of notification come to their phone.</summary>
[Route("api/v1/me")]
[Authorize]
public sealed class InboxController(IInboxService inbox) : ApiControllerBase
{
    [HttpGet("notifications")]
    public async Task<ActionResult<object>> Latest(CancellationToken ct) => Ok(await inbox.LatestAsync(ct));

    [HttpPost("notifications/read")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Read(MarkReadRequest request, CancellationToken ct)
    {
        await inbox.MarkReadAsync(request, ct);
        return Ok<object?>(null, "Marked as read.");
    }

    [HttpGet("notification-settings")]
    public async Task<ActionResult<object>> Settings(CancellationToken ct) => Ok(await inbox.SettingsAsync(ct));

    [HttpPut("notification-settings")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> SetSettings(SetPushSettingsRequest request, CancellationToken ct) =>
        Ok(await inbox.SetSettingsAsync(request, ct), "Notification settings saved.");
}
