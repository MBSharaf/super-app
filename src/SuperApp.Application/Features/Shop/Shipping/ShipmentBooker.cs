using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Shipping;

/// <summary>
/// Books a parcel with the active courier (FR-SHIP-02): a store's part of an order, all printed items of an order from
/// the factory, or a return pickup. Cash on delivery collects the parcel's share of the order (after codes) and the
/// delivery fee once, on the first parcel.
/// </summary>
public sealed class ShipmentBooker(IShippingProviders providers, IShipmentRepository shipments, IClock clock)
{
    public async Task<Shipment> BookAsync(Order order, long? fulfilmentId, long? factoryId, decimal itemsValue, int pieces, string receiver,
        (string Name, string Address) sender, CancellationToken ct)
    {
        var provider = await providers.ActiveAsync(ct);
        var first = await shipments.OpenCountAsync(order.Id, ct) == 0;
        var cod = order.PaymentMethod == OrderPaymentMethod.CashOnDelivery ? CodShare(order, itemsValue, first) : 0;
        var booking = await provider.CreateAsync(new ShipmentRequest(order.Code, receiver, order.Phone, order.AddressJson, cod, pieces, sender.Name, sender.Address, false), ct);
        return Add(order.Id, fulfilmentId, factoryId, provider, booking, cod, false, "Parcel booked with the courier");
    }

    /// <summary>A reverse pickup: the courier collects the returned items from the customer.</summary>
    public async Task<Shipment> BookReturnAsync(Order order, string customerName, int pieces, (string Name, string Address) receiver, CancellationToken ct)
    {
        var provider = await providers.ActiveAsync(ct);
        var booking = await provider.CreateAsync(new ShipmentRequest(order.Code, customerName, order.Phone, order.AddressJson, 0, pieces, receiver.Name, receiver.Address, true), ct);
        return Add(order.Id, null, null, provider, booking, 0, true, "Return pickup booked with the courier");
    }

    /// <summary>The parcel's share of what the customer pays in cash.</summary>
    public static decimal CodShare(Order order, decimal itemsValue, bool first) =>
        (order.Subtotal <= 0 ? 0 : Math.Round((order.Subtotal - order.DiscountAmount) * itemsValue / order.Subtotal, 0, MidpointRounding.AwayFromZero))
        + (first ? order.DeliveryFee : 0);

    private Shipment Add(long orderId, long? fulfilmentId, long? factoryId, IShippingProvider provider, ShipmentBooking booking, decimal cod, bool isReturn, string note)
    {
        var shipment = new Shipment
        {
            OrderId = orderId, FulfilmentId = fulfilmentId, FactoryId = factoryId, Provider = provider.Key, Mode = provider.Mode,
            TrackingNumber = booking.TrackingNumber, CodAmount = cod, Status = booking.Status, IsReturn = isReturn,
        };
        shipment.Events.Add(new ShipmentEvent { Status = (byte)booking.Status, Description = note, At = clock.UtcNow });
        shipments.Add(shipment);
        return shipment;
    }
}
