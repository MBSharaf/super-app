using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Ai;

/// <summary>An agent's permission scope. Table <c>ai.AgentPermissions</c>.</summary>
public class AgentPermission
{
    public long AgentId { get; set; }
    public int PermissionId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Agent Agent { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
