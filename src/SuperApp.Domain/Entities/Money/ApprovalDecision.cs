using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Money;

/// <summary>One person's decision on an approval. Table <c>money.ApprovalDecisions</c>.</summary>
public class ApprovalDecision : BaseEntity
{
    public long ApprovalId { get; set; }
    public Guid DeciderId { get; set; }
    public Decision Decision { get; set; }
    public string? Note { get; set; }

    // Navigation
    public Approval Approval { get; set; } = null!;
    public User Decider { get; set; } = null!;
}
