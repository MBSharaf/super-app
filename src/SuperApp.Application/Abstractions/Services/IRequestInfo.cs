namespace SuperApp.Application.Abstractions.Services;

/// <summary>Facts about the current HTTP request that services may record (IP for legal acceptance and audit).</summary>
public interface IRequestInfo
{
    string? IpAddress { get; }

    /// <summary>The request's correlation id (X-Correlation-Id), to follow one request across logs.</summary>
    string? CorrelationId { get; }
}
