using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Money;

/// <summary>A withdrawal request: requested → prepared → sent. Table <c>money.PayoutRequests</c>.</summary>
public class PayoutRequest : BaseEntity, IHasRowVersion
{
    public string Code { get; set; } = string.Empty;
    public long WalletId { get; set; }
    public decimal Amount { get; set; }
    public PayoutMethod Method { get; set; }
    /// <summary>Encrypted snapshot of the payout account.</summary>
    public string Destination { get; set; } = string.Empty;
    public PayoutStatus Status { get; set; }
    public Guid? PreparedById { get; set; }
    public Guid? SentById { get; set; }
    public string? TransferRef { get; set; }
    public string? RejectReason { get; set; }
    public long? ApprovalId { get; set; }
    public DateTime? SentAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Wallet Wallet { get; set; } = null!;
    public User? PreparedBy { get; set; }
    public User? SentBy { get; set; }
    public Approval? Approval { get; set; }
}
