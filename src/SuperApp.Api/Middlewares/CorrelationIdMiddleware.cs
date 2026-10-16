using Serilog.Context;

namespace SuperApp.Api.Middlewares;

/// <summary>
/// Gives every request a correlation id (from the <c>X-Correlation-Id</c> header or a new one), returns it in the
/// response and adds it to every log line of the request, so one request can be followed across logs.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var id = incoming.Length is > 0 and <= 64 && incoming.All(c => char.IsLetterOrDigit(c) || c == '-') ? incoming : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = id;
        context.Response.Headers[HeaderName] = id;
        using (LogContext.PushProperty("CorrelationId", id))
        {
            await next(context);
        }
    }
}
