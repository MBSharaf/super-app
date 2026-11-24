using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Push;

namespace SuperApp.Api.Controllers;

/// <summary>Phones of the signed-in person that get push notifications (the Android app registers its FCM token here).</summary>
[Route("api/v1/me/push-devices")]
[Authorize]
public sealed class PushDevicesController(IPushDevicesService devices) : ApiControllerBase
{
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Register(RegisterPushDeviceRequest request, CancellationToken ct)
    {
        await devices.RegisterAsync(request, ct);
        return Ok<object?>(null, "Notifications are on for this phone.");
    }

    [HttpPost("remove")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Remove(RemovePushDeviceRequest request, CancellationToken ct)
    {
        await devices.RemoveAsync(request, ct);
        return Ok<object?>(null, "Notifications are off for this phone.");
    }
}
