using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Shipping;

/// <summary>An item of a return request. Table <c>ship.ReturnItems</c>.</summary>
public class ReturnItem : Entity<long>
{
    public long ReturnId { get; set; }
    public long OrderItemId { get; set; }
    public int Qty { get; set; }
    public ReturnAction Action { get; set; }
    public string? NewSize { get; set; }
    public string? NewColor { get; set; }

    // Navigation
    public ReturnRequest Return { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
}
