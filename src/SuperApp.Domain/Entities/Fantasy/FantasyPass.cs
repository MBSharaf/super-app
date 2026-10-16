using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A Fantasy Pass subscription. Table <c>fantasy.FantasyPasses</c>.</summary>
public class FantasyPass : BaseEntity
{
    public Guid UserId { get; set; }
    public int? CompetitionId { get; set; }
    public long? PaymentId { get; set; }
    public decimal Price { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Competition? Competition { get; set; }
    public Payment? Payment { get; set; }
}
