using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Shipping;

/// <summary>A courier shipment (or a reverse pickup). Table <c>ship.Shipments</c>.</summary>
public class Shipment : BaseEntity, IHasRowVersion
{
    public long OrderId { get; set; }
    public long? FulfilmentId { get; set; }
    public long? FactoryId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public IntegrationMode Mode { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public decimal CodAmount { get; set; }
    public long? LabelFileId { get; set; }
    public ShipmentStatus Status { get; set; }
    public DateTime? PickupAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CodRemittedAt { get; set; }
    public bool IsReturn { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Order Order { get; set; } = null!;
    public Fulfilment? Fulfilment { get; set; }
    public Factory? Factory { get; set; }
    public StoredFile? LabelFile { get; set; }
    public ICollection<ShipmentEvent> Events { get; set; } = [];
}
