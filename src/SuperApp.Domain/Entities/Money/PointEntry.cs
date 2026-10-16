using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Money;

/// <summary>Points earned or spent. Table <c>money.PointEntries</c>.</summary>
public class PointEntry : Entity<long>
{
    public Guid UserId { get; set; }
    public int Points { get; set; }
    public PointKind Kind { get; set; }
    public string? RefType { get; set; }
    public long? RefId { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}
