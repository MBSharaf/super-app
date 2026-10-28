using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Orders;

/// <summary>"Every action notifies everyone concerned" for orders: the customer, each store, each seller and the factory.</summary>
public sealed class OrderNotices(INotifier notifier, IAuditLogger audit, ICustomerContacts contacts)
{
    /// <summary>The payment is in: the customer and every store with items in the order are told (the official store is Super App).</summary>
    public async Task PaidAsync(Order order, IReadOnlyList<Money.OrderShare> shares, CancellationToken ct)
    {
        var count = order.Items.Sum(i => i.Qty);
        await notifier.NotifyUserAsync(order.CustomerId, new NotificationMessage(NotificationCategory.Orders, "Order confirmed",
            $"{order.Code} · {count} item{(count > 1 ? "s" : "")} · {order.Total:0} EGP", $"#/order/{order.Code}", NotificationTone.Success), ct);
        foreach (var store in shares.Where(s => s.OwnerUserId is not null).GroupBy(s => s.OwnerUserId!.Value))
        {
            var items = order.Items.Where(i => store.Any(s => s.ItemId == i.Id)).ToList();
            await notifier.NotifyUserAsync(store.Key, new NotificationMessage(NotificationCategory.Orders, $"New store order {order.Code}",
                $"{items.Sum(i => i.Qty)} item(s) · {store.Sum(s => s.Earning):0} EGP for you. Pack it — Super App's courier picks it up.", "#/dash/orders?status=open", NotificationTone.Success), ct);
        }
        await audit.LogAsync(AuditSystem.Site, "order.created", $"Order {order.Code}: {count} item(s), {order.Total:0} EGP", "Order", order.Code,
            Details(order, await contacts.GetAsync(order.CustomerId, ct)), ct);
    }

    /// <summary>What the activity line (and the order.created webhook for n8n) carries: the order and the customer's contact (owner's request).</summary>
    private static object Details(Order order, CustomerContact customer) => new
    {
        order.Code, order.Subtotal, order.DiscountAmount, order.DeliveryFee, order.Total, Payment = order.PaymentMethod.ToString(),
        Delivery = order.DeliverySpeed.ToString(), City = City(order.AddressJson), order.CreatedAt,
        Customer = new { customer.Name, customer.Email, Phone = order.Phone },
        Items = order.Items.Select(i => new { i.Title, i.Size, i.Color, i.Qty, i.UnitPrice, LineTotal = i.UnitPrice * i.Qty, i.StoreId }).ToList(),
    };

    private static string? City(string addressJson)
    {
        try { return System.Text.Json.JsonDocument.Parse(addressJson).RootElement.GetProperty("city").GetString(); }
        catch (Exception e) when (e is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }

    public async Task CancelledAsync(Order order, IReadOnlyList<Guid> people, string by, CancellationToken ct)
    {
        await notifier.NotifyUserAsync(order.CustomerId, new NotificationMessage(NotificationCategory.Orders, $"Order {order.Code} cancelled",
            order.PaymentId is null ? "Nothing to pay." : "The money is back in your Super App wallet.", $"#/order/{order.Code}", NotificationTone.Warning), ct);
        foreach (var person in people.Distinct())
        {
            await notifier.NotifyUserAsync(person, new NotificationMessage(NotificationCategory.Orders, $"Order {order.Code} cancelled",
                $"Cancelled by {by}. Don’t pack or print it.", "#/dash/orders", NotificationTone.Warning), ct);
        }
        await audit.LogAsync(AuditSystem.Site, "order.cancelled", $"Order {order.Code} cancelled by {by}", "Order", order.Code, ct: ct);
    }
}
