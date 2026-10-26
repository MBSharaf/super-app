using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace SuperApp.Api.Security;

/// <summary>
/// Behind nginx on the VPS the connection comes from the proxy, so the client's IP (rate limits, audit, logs) and https
/// are read from X-Forwarded-For / X-Forwarded-Proto. Only trusted proxies may set them: this machine (127.0.0.1, ::1)
/// by default, plus the addresses in <c>ReverseProxy:KnownProxies</c> (comma list, e.g. <c>REVERSEPROXY__KNOWNPROXIES</c>).
/// Anyone else sending these headers is ignored, so a caller cannot pick their own IP.
/// </summary>
public static class ReverseProxy
{
    public static IServiceCollection AddReverseProxy(this IServiceCollection services, IConfiguration configuration)
    {
        var extra = (configuration["ReverseProxy:KnownProxies"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            foreach (var address in extra)
            {
                o.KnownProxies.Add(IPAddress.Parse(address));
            }
        });
        return services;
    }
}
