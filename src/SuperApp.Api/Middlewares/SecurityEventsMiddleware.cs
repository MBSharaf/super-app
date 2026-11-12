namespace SuperApp.Api.Middlewares;

/// <summary>
/// Writes one warning line for every refused request — not signed in (401), not allowed (403), locked (423) or too many
/// requests (429) — with the caller's IP and account, so repeated attempts stand out in the logs
/// (search for "SECURITY"). Server errors and normal traffic are logged elsewhere.
/// </summary>
public sealed class SecurityEventsMiddleware(RequestDelegate next, ILogger<SecurityEventsMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);
        var status = context.Response.StatusCode;
        if (status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden or StatusCodes.Status423Locked or StatusCodes.Status429TooManyRequests)
        {
            logger.LogWarning("SECURITY {Status} {Method} {Path} from {ClientIp} as {User}",
                status, context.Request.Method, context.Request.Path.Value, context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name ?? "signed-in" : "guest");
        }
    }
}
