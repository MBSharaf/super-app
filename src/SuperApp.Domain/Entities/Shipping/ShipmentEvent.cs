using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Shipping;

/// <summary>A courier status update. Table <c>ship.ShipmentEvents</c>.</summary>
public class ShipmentEvent : Entity<long>
{
    public long ShipmentId { get; set; }
    public byte Status { get; set; }
    public string? Description { get; set; }
    public string? RawJson { get; set; }
    public string? ProviderEventId { get; set; }
    public DateTime At { get; set; }

    // Navigation
    public Shipment Shipment { get; set; } = null!;
}
