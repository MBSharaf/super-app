using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Ai;

/// <summary>An API key for an agent or n8n (only the hash is stored). Table <c>ai.ApiKeys</c>.</summary>
public class ApiKey : BaseEntity
{
    public long? AgentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public byte[] KeyHash { get; set; } = [];
    public string? Scopes { get; set; }
    public int RateLimitPerMinute { get; set; } = 600;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    // Navigation
    public Agent? Agent { get; set; }
}
