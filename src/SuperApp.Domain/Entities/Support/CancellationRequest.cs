using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Support;

/// <summary>
/// A customer's request to cancel something they paid for (owner's rule, 2026-11-07): the customer can't cancel a paid
/// booking, package, order or game place directly — the request gets its own number (CX-…), the Super App team (or the
/// AI agent) answers within 2 hours, and only an approval cancels it and refunds. Table <c>support.CancellationRequests</c>.
/// </summary>
public class CancellationRequest : BaseEntity, IHasRowVersion
{
    public string Code { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public CancellationKind Kind { get; set; }
    /// <summary>The booking, package, order or game code.</summary>
    public string Reference { get; set; } = string.Empty;
    /// <summary>What the customer sees ("Turf 22 Arena · 2026-11-10 20:00").</summary>
    public string Label { get; set; } = string.Empty;
    public decimal PaidOnline { get; set; }
    /// <summary>The refund under the policy when the request was made (the real one is worked out on approval).</summary>
    public decimal RefundIfApproved { get; set; }
    public string? Reason { get; set; }
    public CancellationStatus Status { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedById { get; set; }
    public string? DecisionNote { get; set; }
    public decimal? Refunded { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public User Customer { get; set; } = null!;
    public User? DecidedBy { get; set; }
}
