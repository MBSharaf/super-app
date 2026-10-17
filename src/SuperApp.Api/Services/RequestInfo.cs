using SuperApp.Api.Middlewares;
using SuperApp.Application.Abstractions.Services;

namespace SuperApp.Api.Services;

/// <summary>IP address and correlation id of the current request.</summary>
public sealed class RequestInfo(IHttpContextAccessor accessor) : IRequestInfo
{
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? CorrelationId => accessor.HttpContext?.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString() is { Length: > 0 } id ? id : null;
}
