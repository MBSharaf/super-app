using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Money;

/// <summary>A manual credit or debit of a wallet. Table <c>money.Adjustments</c>.</summary>
public class Adjustment : BaseEntity
{
    public long WalletId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid ById { get; set; }
    public long? ApprovalId { get; set; }

    // Navigation
    public Wallet Wallet { get; set; } = null!;
    public User By { get; set; } = null!;
    public Approval? Approval { get; set; }
}
