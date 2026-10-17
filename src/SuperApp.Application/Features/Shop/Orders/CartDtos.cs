using SuperApp.Application.Features.Pitches;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Orders;

/// <summary>
/// One cart line with live data (price, stock, whether it can still be bought). <c>Kind</c> = product | design;
/// <c>ItemId</c> = product slug or design id. <c>Problem</c> explains a line that can't be bought as it is.
/// </summary>
public sealed record CartLineDto(
    long Id,
    string Kind,
    string ItemId,
    long? VariantId,
    long? DesignId,
    string Title,
    PhotoDto? Image,
    StoreRefDto Store,
    string Size,
    string Color,
    int Qty,
    decimal UnitPrice,
    decimal? CompareAtPrice,
    int? Available,
    bool CanBuy,
    string? Problem);

/// <summary>An applied promo code. Funding: SuperApp (Super App pays) or Store (the store pays, only its items).</summary>
public sealed record PromoDto(string Code, string? Label, string Funding, decimal Discount, bool FreeDelivery, string? StoreHandle);

/// <summary>
/// GET /cart. <c>DeliveryFee</c> is for the chosen speed (standard unless asked); <c>ExpressFee</c> shows what express
/// costs. Promo codes are not stored with the cart: send <c>?promo=CODE</c> (POST /cart/promo checks one).
/// </summary>
public sealed record CartDto(
    IReadOnlyList<CartLineDto> Lines,
    int Count,
    decimal Subtotal,
    decimal Savings,
    decimal Discount,
    decimal DeliveryFee,
    decimal StandardFee,
    decimal ExpressFee,
    decimal Total,
    decimal FreeDeliveryOver,
    decimal FreeDeliveryGap,
    PromoDto? Promo,
    string? PromoError);

/// <summary>Add a store product variant, or a printed design with its size and colour.</summary>
public sealed record AddCartItemRequest(long? ProductVariantId, long? DesignId, string? Size, string? Color, int Qty = 1);

public sealed record UpdateCartItemRequest(int Qty);

public sealed record PromoRequest(string Code);

/// <summary>Save a product (by slug) or a printed design to the wishlist.</summary>
public sealed record WishlistRequest(string? ProductSlug, long? DesignId);

/// <summary>A saved item; <c>Card</c> is null when the item is no longer on sale.</summary>
public sealed record WishlistItemDto(long Id, string Kind, string ItemId, ShopCardDto? Card);

/// <summary>Delivery fees (settings "delivery.fees"): standard, express, free over an amount.</summary>
public sealed record DeliveryFees(decimal Standard, decimal Express, decimal FreeOver);

/// <summary>A cart line with what checkout needs to price, route and pay everyone (internal, never sent as is).</summary>
public sealed record CartLine(
    long Id,
    OrderItemKind Kind,
    string ItemId,
    long? VariantId,
    long? DesignId,
    string Title,
    PhotoDto? Image,
    long StoreId,
    string StoreHandle,
    string StoreName,
    StoreType StoreType,
    Guid? StoreOwnerId,
    string Size,
    string Color,
    int Qty,
    decimal UnitPrice,
    decimal? CompareAt,
    int Available,
    string? Problem,
    decimal? CommissionRate,
    PrintedLine? Printed)
{
    public bool CanBuy => Problem is null;

    public decimal LineTotal => UnitPrice * Qty;
}

/// <summary>
/// Printed-design pricing: <c>BaseUnit</c> = catalogue base + back print (if printed) + big size − the seller's level
/// discount (FR-POD-04); <c>FactoryCost</c> = the catalogue cost per piece (the factory's own price at checkout).
/// </summary>
public sealed record PrintedLine(int CatalogProductId, decimal BaseUnit, decimal FactoryCost, bool BackPrinted, long SellerId, Guid SellerUserId);

/// <summary>A valid promo code for this cart and what it takes off.</summary>
public sealed record PromoResult(long PromoId, string Code, string? Label, CodeFunding Funding, long? StoreId, string? StoreHandle, decimal Discount, bool FreeDelivery);
