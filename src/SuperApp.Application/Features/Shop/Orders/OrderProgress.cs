using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Orders;

/// <summary>The order's overall status follows its items (each store and the factory move their own items).</summary>
public static class OrderProgress
{
    public static void Update(Order order)
    {
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Refunded)
        {
            return;
        }
        if (order.Items.All(i => i.Status == OrderItemStatus.Cancelled))
        {
            order.Status = OrderStatus.Cancelled;
            return;
        }
        var open = order.Items.Where(i => i.Status != OrderItemStatus.Cancelled).ToList();
        if (open.All(i => i.Status is OrderItemStatus.Delivered or OrderItemStatus.Returned or OrderItemStatus.Refused))
        {
            order.Status = OrderStatus.Completed;
        }
        else if (open.Any(i => i.Status != OrderItemStatus.Received))
        {
            order.Status = OrderStatus.InProgress;
        }
    }
}
