using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Shipping;

/// <summary>What the courier needs to collect and deliver one parcel.</summary>
public sealed record ShipmentRequest(string OrderCode, string ReceiverName, string Phone, string AddressJson, decimal CodAmount, int Pieces, string SenderName, string SenderAddress, bool IsReturn);

/// <summary>The courier's answer: its tracking number (airway bill) and the first status.</summary>
public sealed record ShipmentBooking(string TrackingNumber, ShipmentStatus Status);

/// <summary>
/// One courier company (FR-SHIP-01, INTEGRATIONS "Courier"). Adding a company = one adapter class; the active one and
/// its mode come from settings ("delivery.courier"). Until a contract exists the simulated courier is used.
/// </summary>
public interface IShippingProvider
{
    /// <summary>Integration key, e.g. "courier.simulated", "courier.bosta".</summary>
    string Key { get; }

    IntegrationMode Mode { get; }

    Task<ShipmentBooking> CreateAsync(ShipmentRequest request, CancellationToken ct);

    Task CancelAsync(string trackingNumber, CancellationToken ct);

    /// <summary>Checks a webhook's signature; the simulated courier signs with the server's own key.</summary>
    bool IsSignatureValid(string body, string? signature);
}

/// <summary>Picks the active courier (settings "delivery.courier": provider key); unknown keys fall back to the simulated courier.</summary>
public interface IShippingProviders
{
    Task<IShippingProvider> ActiveAsync(CancellationToken ct);

    IShippingProvider? ByKey(string key);
}

/// <summary>Parcels and their status history.</summary>
public interface IShipmentRepository
{
    void Add(Shipment shipment);

    Task<Shipment?> FindAsync(long id, CancellationToken ct);

    Task<Shipment?> ByTrackingAsync(string provider, string trackingNumber, CancellationToken ct);

    Task<bool> EventSeenAsync(string providerEventId, CancellationToken ct);

    /// <summary>Open parcels of this order (not cancelled), to know whether the delivery fee is already collected.</summary>
    Task<int> OpenCountAsync(long orderId, CancellationToken ct);
}
