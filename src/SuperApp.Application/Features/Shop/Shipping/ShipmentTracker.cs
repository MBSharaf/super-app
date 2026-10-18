using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Shop.Orders;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Shipping;

/// <summary>Everything a parcel status change touches: its items, the store parcel, print jobs, sales counts and levels.</summary>
public interface IParcelRepository
{
    /// <summary>The parcel's order with items, parcels and print jobs, tracked for changes.</summary>
    Task<Order> OrderAsync(long orderId, CancellationToken ct);

    Task<IReadOnlyList<Domain.Entities.Factories.Job>> JobsAsync(long orderId, CancellationToken ct);

    /// <summary>Adds delivered pieces to the designs and sellers, and moves sellers up a level when they qualify (level-ups are immediate).</summary>
    Task<IReadOnlyList<(Guid UserId, string LevelName)>> CountSalesAsync(IReadOnlyList<OrderItem> delivered, CancellationToken ct);

    Task CountRefusalAsync(Guid customerId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> StoreOwnersAsync(IReadOnlyList<long> storeIds, CancellationToken ct);
}

/// <summary>
/// Applies a courier status to a parcel (FR-FAC-06, FR-SHIP-02): picked up → items shipped; delivered → items delivered (money
/// becomes due after the hold days); refused → items refused, store stock back, the customer's refusals counted for COD.
/// A courier event id seen before is ignored.
/// </summary>
public sealed class ShipmentTracker(IShipmentRepository shipments, IParcelRepository parcels, IOrderRepository orders, IUnitOfWork unitOfWork, INotifier notifier, IClock clock)
{
    public async Task<bool> ApplyAsync(Shipment shipment, ShipmentStatus status, string? description, string? eventId, string? raw, CancellationToken ct)
    {
        if (eventId is not null && await shipments.EventSeenAsync(eventId, ct))
        {
            return false;
        }
        shipment.Events.Add(new ShipmentEvent { Status = (byte)status, Description = description, ProviderEventId = eventId, RawJson = raw, At = clock.UtcNow });
        if (shipment.Status == status || shipment.Status is ShipmentStatus.Delivered or ShipmentStatus.Refused or ShipmentStatus.Cancelled)
        {
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }
        shipment.Status = status;
        shipment.PickupAt = status == ShipmentStatus.PickedUp ? clock.UtcNow : shipment.PickupAt;
        shipment.DeliveredAt = status == ShipmentStatus.Delivered ? clock.UtcNow : shipment.DeliveredAt;
        if (!shipment.IsReturn)
        {
            await MoveItemsAsync(shipment, status, ct);
        }
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task MoveItemsAsync(Shipment shipment, ShipmentStatus status, CancellationToken ct)
    {
        var order = await parcels.OrderAsync(shipment.OrderId, ct);
        var parcel = shipment.FulfilmentId is { } fid ? order.Fulfilments.Single(f => f.Id == fid) : null;
        var items = order.Items.Where(i => i.Status != OrderItemStatus.Cancelled
            && (parcel is null ? i.Kind == OrderItemKind.PrintedDesign : i.StoreId == parcel.StoreId && i.Kind == OrderItemKind.StoreProduct)).ToList();
        switch (status)
        {
            case ShipmentStatus.PickedUp:
                Set(items, OrderItemStatus.Shipped);
                foreach (var job in (await parcels.JobsAsync(order.Id, ct)).Where(j => j.Stage == Domain.Enums.JobStage.Ready)) { job.Stage = Domain.Enums.JobStage.Shipped; }
                await TellAsync(order, "Your order is on its way", $"The courier picked up your parcel · {shipment.TrackingNumber}", ct);
                break;
            case ShipmentStatus.OutForDelivery:
                await TellAsync(order, "Out for delivery today", $"{order.Code} · have {(shipment.CodAmount > 0 ? $"{shipment.CodAmount:0} EGP ready" : "your phone nearby")}", ct);
                break;
            case ShipmentStatus.Delivered:
                await DeliveredAsync(order, parcel, items, ct);
                break;
            case ShipmentStatus.Refused:
                await RefusedAsync(order, parcel, items, ct);
                break;
            case ShipmentStatus.Cancelled:
                Set(items.Where(i => i.Status == OrderItemStatus.Shipped).ToList(), OrderItemStatus.Packed);
                if (parcel is not null) { parcel.Status = FulfilmentStatus.Packed; }
                break;
        }
        OrderProgress.Update(order);
    }

    private async Task DeliveredAsync(Order order, Fulfilment? parcel, List<OrderItem> items, CancellationToken ct)
    {
        Set(items, OrderItemStatus.Delivered);
        if (parcel is not null) { parcel.Status = FulfilmentStatus.Delivered; }
        foreach (var (userId, level) in await parcels.CountSalesAsync(items, ct))
        {
            await notifier.NotifyUserAsync(userId, new NotificationMessage(NotificationCategory.Level, $"You reached {level}!", "Your new level’s benefits are on now.", "#/seller", NotificationTone.Success), ct);
        }
        await TellAsync(order, "Delivered", $"{order.Code} was delivered. Enjoy! You can return items within 14 days.", ct);
        await TellSellersAsync(order, $"{order.Code} delivered", "Your earnings become available after the hold days.", ct);
    }

    private async Task RefusedAsync(Order order, Fulfilment? parcel, List<OrderItem> items, CancellationToken ct)
    {
        Set(items, OrderItemStatus.Refused);
        if (parcel is not null) { parcel.Status = FulfilmentStatus.Cancelled; }
        await orders.RestockAsync(items.Where(i => i.ProductVariantId is not null).Select(i => (i.ProductVariantId!.Value, i.Qty)).ToList(), ct);
        if (order.PaymentMethod == OrderPaymentMethod.CashOnDelivery)
        {
            await parcels.CountRefusalAsync(order.CustomerId, ct);
        }
        await TellAsync(order, "Delivery refused", $"The parcel of {order.Code} was refused and goes back.", ct);
        await TellSellersAsync(order, $"{order.Code} refused", "The customer refused the parcel. It comes back to you.", ct);
    }

    private void Set(IReadOnlyList<OrderItem> items, OrderItemStatus status)
    {
        foreach (var item in items)
        {
            item.Status = status;
            item.DeliveredAt = status == OrderItemStatus.Delivered ? clock.UtcNow : item.DeliveredAt;
        }
    }

    private async Task TellSellersAsync(Order order, string title, string body, CancellationToken ct)
    {
        var sellers = order.Items.Where(i => i.Kind == OrderItemKind.PrintedDesign && i.DesignId is not null).Select(i => i.StoreId).Distinct().ToList();
        foreach (var person in await orders.PeopleToTellAsync(order.Id, ct))
        {
            await notifier.NotifyUserAsync(person, new NotificationMessage(NotificationCategory.Orders, title, body, "#/dash/orders"), ct);
        }
        foreach (var owner in await parcels.StoreOwnersAsync(sellers, ct))
        {
            await notifier.NotifyUserAsync(owner, new NotificationMessage(NotificationCategory.Orders, title, body, "#/seller/orders"), ct);
        }
    }

    private Task TellAsync(Order order, string title, string body, CancellationToken ct) =>
        notifier.NotifyUserAsync(order.CustomerId, new NotificationMessage(NotificationCategory.Orders, title, body, $"#/order/{order.Code}"), ct);
}
