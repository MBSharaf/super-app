using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Auth;

/// <summary>Money limits of an admin, the CFO or an agent's service account. Table <c>auth.StaffLimits</c>.</summary>
public class StaffLimit : BaseEntity
{
    public Guid UserId { get; set; }
    public decimal RefundLimit { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal PayoutLimit { get; set; }
    public bool CanSecondApprove { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
