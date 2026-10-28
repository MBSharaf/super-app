using SuperApp.Application.Features.Pitches;
using SuperApp.Application.Features.Shop.Catalog;

namespace SuperApp.Application.Features.Shop.Orders;

/// <summary>A delivery address typed at checkout (snapshot on the order).</summary>
public sealed record CheckoutAddress(string? Label, string Line1, string? Line2, string City, int? AreaId);

/// <summary>
/// POST /orders/checkout with an <c>Idempotency-Key</c> header. Address: a saved one (<c>AddressId</c>) or typed.
/// DeliverySpeed: standard | express. PaymentMethod: online | cod | wallet (online and wallet are simulated until Phase 5).
/// </summary>
public sealed record CheckoutRequest(
    long? AddressId,
    CheckoutAddress? Address,
    string Phone,
    string DeliverySpeed = "standard",
    string PaymentMethod = "online",
    string? PromoCode = null);

/// <summary>The placed order and how it is paid (Simulated = no real money moved yet).</summary>
/// <summary>PaymentUrl: the gateway's page to finish paying (null = already paid by the Super App wallet or the simulated gateway).</summary>
public sealed record CheckoutResultDto(string Code, string Status, string PaymentMethod, bool PaymentSimulated, decimal Total, OrderDetailsDto Order, string? PaymentUrl = null);

/// <summary>An order in "My orders".</summary>
public sealed record OrderSummaryDto(string Code, DateTime PlacedAt, string Status, int Items, decimal Total, string PaymentMethod, PhotoDto? Image, string FirstTitle);

/// <summary>One item of an order. Kind: product | design. Status: Received, Printing, Packed, Shipped, Delivered, Refused, Cancelled, Returned.</summary>
public sealed record OrderItemDto(long Id, string Kind, string ItemId, string Title, PhotoDto? Image, string Size, string Color, int Qty, decimal UnitPrice,
    decimal LineTotal, string Status, DateTime? DeliveredAt);

/// <summary>A parcel of the order: one per store, and one for printed items (made and shipped by the factory).</summary>
public sealed record OrderGroupDto(string Key, StoreRefDto? Store, bool Printed, string Status, string? Courier, string? TrackingNumber, IReadOnlyList<OrderItemDto> Items);

/// <summary>GET /orders/{code}. Status Placed = waiting for the payment (called off after 15 minutes); Paid = the payment went through.</summary>
public sealed record OrderDetailsDto(
    string Code,
    DateTime PlacedAt,
    string Status,
    string PaymentMethod,
    string DeliverySpeed,
    string AddressJson,
    string Phone,
    decimal Subtotal,
    decimal Discount,
    decimal DeliveryFee,
    decimal Total,
    decimal VatAmount,
    string? PromoCode,
    bool CanCancel,
    IReadOnlyList<OrderGroupDto> Groups,
    bool Paid = false);

/// <summary>GET /orders/mine filters: status = open | completed | cancelled | all.</summary>
public sealed record MyOrdersQuery(string? Status = null, int Page = 1, int PageSize = 20);
