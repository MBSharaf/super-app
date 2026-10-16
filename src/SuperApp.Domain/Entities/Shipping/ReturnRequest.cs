using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Shipping;

/// <summary>A customer's return or exchange request. Table <c>ship.Returns</c>.</summary>
public class ReturnRequest : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public long OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public ReturnReason Reason { get; set; }
    public string? Detail { get; set; }
    public ReturnStatus Status { get; set; }
    public Guid? DecidedById { get; set; }
    public string? RejectReason { get; set; }
    public decimal RefundAmount { get; set; }
    public bool CustomerPaysDelivery { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
    public User Customer { get; set; } = null!;
    public User? DecidedBy { get; set; }
    public ICollection<ReturnItem> Items { get; set; } = [];
}
