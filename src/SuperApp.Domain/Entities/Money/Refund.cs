using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Money;

/// <summary>A refund of a payment. Table <c>money.Refunds</c>.</summary>
public class Refund : BaseEntity
{
    public long PaymentId { get; set; }
    public decimal Amount { get; set; }
    public RefundDestination Destination { get; set; }
    public RefundPayer PaidBy { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ProviderRef { get; set; }
    public RefundStatus Status { get; set; }
    public Guid RequestedById { get; set; }
    public long? ApprovalId { get; set; }

    // Navigation
    public Payment Payment { get; set; } = null!;
    public User RequestedBy { get; set; } = null!;
    public Approval? Approval { get; set; }
}
