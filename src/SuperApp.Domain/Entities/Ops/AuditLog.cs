using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>Append-only company audit log. Table <c>ops.AuditLogs</c>.</summary>
public class AuditLog : Entity<long>
{
    public DateTime At { get; set; } = DateTime.UtcNow;
    public Guid? ActorUserId { get; set; }
    public long? ActorAgentId { get; set; }
    public AuditSystem System { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? DataJson { get; set; }
    public string? Ip { get; set; }
    public string? CorrelationId { get; set; }

    // Navigation
    public User? ActorUser { get; set; }
    public Agent? ActorAgent { get; set; }
}
