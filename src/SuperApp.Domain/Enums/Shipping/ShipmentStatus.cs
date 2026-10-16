namespace SuperApp.Domain.Enums;

public enum ShipmentStatus : byte
{
    Created = 0,
    AwaitingPickup = 1,
    PickedUp = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Refused = 5,
    Cancelled = 6,
}
