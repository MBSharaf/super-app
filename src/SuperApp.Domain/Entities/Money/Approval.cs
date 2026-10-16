using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Money;

/// <summary>Something over a money limit waiting for a human decision. Table <c>money.Approvals</c>.</summary>
public class Approval : BaseEntity
{
    public ApprovalKind Kind { get; set; }
    public string RefType { get; set; } = string.Empty;
    public long RefId { get; set; }
    public decimal Amount { get; set; }
    public string Summary { get; set; } = string.Empty;
    public Guid RequestedById { get; set; }
    public byte RequiredApprovals { get; set; } = (byte)1;
    public ApprovalStatus Status { get; set; }
    public DateTime? DecidedAt { get; set; }

    // Navigation
    public User RequestedBy { get; set; } = null!;
    public ICollection<ApprovalDecision> Decisions { get; set; } = [];
}
