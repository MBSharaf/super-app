using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>A seller's sample order of their own design. Table <c>pod.Samples</c>.</summary>
public class Sample : BaseEntity
{
    public long SellerId { get; set; }
    public long DesignId { get; set; }
    public string Color { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsFree { get; set; }
    public long? OrderId { get; set; }
    public SampleStatus Status { get; set; }

    // Navigation
    public Seller Seller { get; set; } = null!;
    public Design Design { get; set; } = null!;
    public Order? Order { get; set; }
}
