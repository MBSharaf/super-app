using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Shop;

/// <summary>The part of an order one store ships. Table <c>shop.Fulfilments</c>.</summary>
public class Fulfilment : BaseEntity
{
    public long OrderId { get; set; }
    public long StoreId { get; set; }
    public FulfilmentStatus Status { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
    public Store Store { get; set; } = null!;
}
