using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Stores;

/// <summary>The store the caller runs: a pitch partner's shop, a seller's store, or the official store (staff).</summary>
public sealed record StoreScope(long StoreId, StoreType Type, Guid? OwnerUserId, string Handle, string Name, AuditSystem System, string Who);

/// <summary>GET /partner/store, /seller/store — the store profile.</summary>
public sealed record StoreProfileDto(
    string Handle,
    string Name,
    string Type,
    string? Tagline,
    string? About,
    string AccentColor,
    int CoverPositionY,
    PhotoDto? Logo,
    PhotoDto? Cover,
    string Status,
    DateOnly? BreakUntil,
    string? BreakNote,
    string? Mode,
    long? FeaturedDesignId);

/// <summary>
/// PUT store profile. Handle: lowercase letters, numbers and dashes (409 handle_taken). A break (until a date) hides the
/// store's items; <c>Mode</c> (sellers): pod | store | both; <c>FeaturedDesignId</c> (sellers): one of their live designs.
/// </summary>
public sealed record StoreProfileRequest(
    string Name,
    string Handle,
    string? Tagline,
    string? About,
    string? AccentColor,
    int CoverPositionY = 50,
    DateOnly? BreakUntil = null,
    string? BreakNote = null,
    string? Mode = null,
    long? FeaturedDesignId = null);

public sealed record SpecRequest(string Label, string Value);

/// <summary>A size / colour of a product. Send the id of an existing variant to change it; variants left out are removed.</summary>
public sealed record VariantRequest(long? Id, string Size, string ColorName, string? ColorHex, string? Sku, int Stock, decimal? PriceOverride);

/// <summary>Add or edit a store product. Status: Draft, Published or Unpublished.</summary>
public sealed record ProductRequest(
    string Name,
    string CategoryKey,
    string? Brand,
    string? Description,
    decimal Price,
    decimal? CompareAtPrice,
    int LowStockAt,
    string Status,
    IReadOnlyList<SpecRequest> Specs,
    IReadOnlyList<VariantRequest> Variants);

public sealed record OwnVariantDto(long Id, string Size, string ColorName, string? ColorHex, string Sku, int Stock, int Reserved, decimal? PriceOverride);

public sealed record OwnImageDto(long Id, PhotoDto Photo, int SortOrder);

/// <summary>A product as its store sees it (any status), with stock per variant.</summary>
public sealed record OwnProductDto(
    long Id,
    string Slug,
    string Name,
    string CategoryKey,
    string? Brand,
    string? Description,
    decimal Price,
    decimal? CompareAtPrice,
    int LowStockAt,
    string Status,
    bool HiddenByAdmin,
    string? HiddenReason,
    string? ReviewNote,
    int Stock,
    bool LowStock,
    IReadOnlyList<OwnImageDto> Images,
    IReadOnlyList<OwnVariantDto> Variants,
    IReadOnlyList<SpecRequest> Specs,
    DateTime CreatedAt);

public sealed record RestockRequest(long VariantId, int Add);

/// <summary>GET /admin/products/queue — a product waiting for review, with its store and photos.</summary>
public sealed record ReviewProductDto(long Id, string Slug, string Name, string? Brand, string? Description, string Category, decimal Price,
    decimal? CompareAtPrice, string Store, string StoreType, string StoreHandle, IReadOnlyList<Pitches.PhotoDto> Photos, IReadOnlyList<SpecRequest> Specs,
    int Variants, int Stock, DateTime CreatedAt);

public sealed record ImageOrderRequest(IReadOnlyList<long> ImageIds);

/// <summary>Store orders filter: status = open | new | packed | shipped | delivered | cancelled | all; q = order code, name or phone.</summary>
public sealed record StoreOrdersQuery(string? Status = null, string? Q = null, int Page = 1, int PageSize = 20);

public sealed record StoreOrderItemDto(long Id, string Title, string Size, string Color, int Qty, decimal UnitPrice, decimal Commission, decimal Earning, string Status, PhotoDto? Image);

/// <summary>The store's part of an order (one parcel). Status: New, Packed, Shipped, Delivered, Cancelled.</summary>
public sealed record StoreOrderDto(
    string OrderCode,
    DateTime PlacedAt,
    string Status,
    string CustomerName,
    string Phone,
    string AddressJson,
    string PaymentMethod,
    string DeliverySpeed,
    decimal ItemsTotal,
    decimal Earnings,
    string? Courier,
    string? TrackingNumber,
    string? ShipmentStatus,
    IReadOnlyList<StoreOrderItemDto> Items);

public sealed record CancelReasonRequest(string Reason);

/// <summary>Admin: hide one product with a reason (FR-SHOP-10).</summary>
public sealed record HideProductRequest(string Reason);
