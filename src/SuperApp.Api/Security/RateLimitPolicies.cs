using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Application.Common.Responses;

namespace SuperApp.Api.Security;

/// <summary>
/// Request limits per caller (PRD NFR-SEC-04). Controllers pick one with <c>[EnableRateLimiting(RateLimitPolicies.Auth)]</c>;
/// everything else falls under the global read limit. Signed-in users are counted by account, guests by IP address.
/// </summary>
public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Scan = "scan";
    public const string Write = "write";
    public const string Chat = "chat";
    public const string Upload = "upload";
    public const string Agent = "agent";

    public static IServiceCollection AddSuperAppRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = (ctx, token) => new ValueTask(ctx.HttpContext.Response.WriteAsJsonAsync(
                ApiResponse.Fail("Too many requests. Please wait a moment and try again.",
                    new Dictionary<string, string[]> { ["code"] = ["rate_limited"] }, ctx.HttpContext.TraceIdentifier), token));
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => PerMinute(ctx, "read", 300));
            Add(o, Auth, 10);
            Add(o, Scan, 60);
            Add(o, Write, 60);
            Add(o, Chat, 30);
            Add(o, Upload, 20);
            Add(o, Agent, 30);
        });
        return services;
    }

    private static void Add(RateLimiterOptions o, string name, int perMinute) =>
        o.AddPolicy(name, ctx => PerMinute(ctx, name, perMinute));

    private static RateLimitPartition<string> PerMinute(HttpContext ctx, string name, int limit) =>
        RateLimitPartition.GetFixedWindowLimiter($"{name}:{CallerKey(ctx)}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });

    private static string CallerKey(HttpContext ctx) =>
        ctx.User.Identity?.IsAuthenticated == true && ctx.User.Identity.Name is { } user
            ? $"u:{user}"
            : $"ip:{ctx.Connection.RemoteIpAddress}";
}
