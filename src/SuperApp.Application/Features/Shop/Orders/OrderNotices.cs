using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Orders;

/// <summary>"Every action notifies everyone concerned" for orders: the customer, each store, each seller and the factory.</summary>
public sealed class OrderNotices(INotifier notifier, IAuditLogger audit)
{
    public async Task PlacedAsync(Order order, IReadOnlyList<CartLine> lines, Guid? factoryOwner, CancellationToken ct)
    {
        var count = order.Items.Sum(i => i.Qty);
        await notifier.NotifyUserAsync(order.CustomerId, new NotificationMessage(NotificationCategory.Orders, "Order confirmed",
            $"{order.Code} · {count} item{(count > 1 ? "s" : "")} · {order.Total:0} EGP", $"#/order/{order.Code}", NotificationTone.Success), ct);
        foreach (var store in lines.Where(l => l.Printed is null && l.StoreType != StoreType.Official && l.StoreOwnerId is not null).GroupBy(l => l.StoreOwnerId!.Value))
        {
            await notifier.NotifyUserAsync(store.Key, new NotificationMessage(NotificationCategory.Orders, $"New store order {order.Code}",
                $"{store.Sum(l => l.Qty)} item(s) · {store.Sum(l => l.LineTotal):0} EGP. Pack and ship it to get paid.", "#/dash/orders?status=open", NotificationTone.Success), ct);
        }
        foreach (var seller in lines.Where(l => l.Printed is not null).GroupBy(l => l.Printed!.SellerUserId))
        {
            await notifier.NotifyUserAsync(seller.Key, new NotificationMessage(NotificationCategory.Orders, "You made a sale",
                $"{string.Join(", ", seller.Select(l => l.Title).Distinct())} · {order.Code}", "#/seller/orders", NotificationTone.Success), ct);
        }
        if (factoryOwner is { } owner)
        {
            var printed = order.Items.Count(i => i.Kind == OrderItemKind.PrintedDesign);
            await notifier.NotifyUserAsync(owner, new NotificationMessage(NotificationCategory.Orders, $"{printed} new print job(s)", $"Order {order.Code}", "#/factory"), ct);
        }
        await audit.LogAsync(AuditSystem.Site, "order.created", $"Order {order.Code}: {count} item(s), {order.Total:0} EGP", "Order", order.Code, ct: ct);
    }

    public async Task CancelledAsync(Order order, IReadOnlyList<Guid> people, string by, CancellationToken ct)
    {
        await notifier.NotifyUserAsync(order.CustomerId, new NotificationMessage(NotificationCategory.Orders, $"Order {order.Code} cancelled",
            order.PaymentMethod == OrderPaymentMethod.CashOnDelivery ? "Nothing to pay." : "Your refund goes back to you.", $"#/order/{order.Code}", NotificationTone.Warning), ct);
        foreach (var person in people.Distinct())
        {
            await notifier.NotifyUserAsync(person, new NotificationMessage(NotificationCategory.Orders, $"Order {order.Code} cancelled",
                $"Cancelled by {by}. Don’t pack or print it.", "#/dash/orders", NotificationTone.Warning), ct);
        }
        await audit.LogAsync(AuditSystem.Site, "order.cancelled", $"Order {order.Code} cancelled by {by}", "Order", order.Code, ct: ct);
    }
}
