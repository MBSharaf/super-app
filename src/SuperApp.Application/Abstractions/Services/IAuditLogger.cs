using SuperApp.Domain.Enums;

namespace SuperApp.Application.Abstractions.Services;

/// <summary>Writes one line to the company audit log (who did what, from which system, on which record).</summary>
public interface IAuditLogger
{
    Task LogAsync(AuditSystem system, string action, string summary, string? entityType = null, string? entityId = null,
        object? data = null, CancellationToken ct = default);
}
