using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Factories;

/// <summary>A registered factory device (tablet, scanner PC). Table <c>factory.FactoryDevices</c>.</summary>
public class FactoryDevice : BaseEntity
{
    public long FactoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte[] TokenHash { get; set; } = [];
    public DateTime? LastSeenAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    // Navigation
    public Factory Factory { get; set; } = null!;
}
