using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Orders;

/// <summary>The customer's cart and wishlist in the database (shop.CartItems, shop.WishlistItems).</summary>
public interface ICartRepository
{
    /// <summary>Every cart line with live price, stock and whether it can still be bought.</summary>
    Task<IReadOnlyList<CartLine>> LinesAsync(Guid userId, CancellationToken ct);

    Task<CartItem?> ItemAsync(Guid userId, long id, CancellationToken ct);

    Task<CartItem?> SameItemAsync(Guid userId, long? variantId, long? designId, string? size, string? color, CancellationToken ct);

    void Add(CartItem item);

    void Remove(CartItem item);

    /// <summary>A variant that can be bought: its stock left, or null when the product is not on sale.</summary>
    Task<int?> VariantAvailableAsync(long variantId, CancellationToken ct);

    /// <summary>A live design's sizes and active colours, or null when it is not on sale.</summary>
    Task<(IReadOnlyList<string> Sizes, IReadOnlyList<string> Colors)?> DesignOptionsAsync(long designId, CancellationToken ct);

    Task<PromoCode?> PromoAsync(string code, CancellationToken ct);

    Task<int> PromoUsesAsync(long promoId, Guid userId, CancellationToken ct);

    Task<string?> StoreHandleAsync(long storeId, CancellationToken ct);

    Task<IReadOnlyList<WishlistItemDto>> WishlistAsync(Guid userId, CancellationToken ct);

    /// <summary>Saves the item (once); false when the product or design does not exist.</summary>
    Task<bool> AddWishAsync(Guid userId, string? productSlug, long? designId, CancellationToken ct);

    Task<bool> RemoveWishAsync(Guid userId, long id, CancellationToken ct);
}

/// <summary>Orders, their items, parcels and production jobs.</summary>
public interface IOrderRepository
{
    Task<string> NewCodeAsync(CancellationToken ct);

    /// <summary>The code of the customer's order placed with this Idempotency-Key, if any.</summary>
    Task<string?> CodeByKeyAsync(Guid customerId, string idempotencyKey, CancellationToken ct);

    Task<string> NewJobCodeAsync(CancellationToken ct);

    /// <summary>The factory that takes the next printed items (active, intake open, least busy).</summary>
    Task<(long Id, Guid OwnerUserId, short SlaHours)?> AcceptingFactoryAsync(CancellationToken ct);

    /// <summary>The factory's price per piece for the product (+ back print), or null when it has none.</summary>
    Task<decimal?> FactoryPriceAsync(long factoryId, int catalogProductId, bool backPrinted, CancellationToken ct);

    /// <summary>The customer's cash-on-delivery rule, refused deliveries so far (FR-POD-10) and account status.</summary>
    Task<(PrepayRule Rule, int Refusals, UserStatus Status)> CustomerRulesAsync(Guid customerId, CancellationToken ct);

    /// <summary>A saved address of the customer as a JSON snapshot with its phone, or null.</summary>
    Task<(string Json, string Phone)?> AddressAsync(Guid userId, long addressId, CancellationToken ct);

    /// <summary>
    /// Saves the order, its jobs and the code use, takes the stock and empties the bought cart lines — all or nothing.
    /// Stock that is no longer there → ConflictException (409 stock_changed).
    /// </summary>
    Task PlaceAsync(Order order, IReadOnlyList<Job> jobs, IReadOnlyList<(long VariantId, int Qty)> stock, PromoCodeUse? promoUse,
        IReadOnlyList<long> cartLineIds, CancellationToken ct);

    /// <summary>The customer's order with items, parcels and jobs, tracked for changes.</summary>
    Task<Order?> FindAsync(Guid customerId, string code, CancellationToken ct);

    Task<IReadOnlyList<Job>> JobsAsync(long orderId, CancellationToken ct);

    Task<PagedResult<OrderSummaryDto>> MineAsync(Guid customerId, MyOrdersQuery query, CancellationToken ct);

    Task<OrderDetailsDto?> DetailsAsync(Guid customerId, string code, CancellationToken ct);

    /// <summary>Puts the stock of these variants back (cancelled or returned items).</summary>
    Task RestockAsync(IReadOnlyList<(long VariantId, int Qty)> stock, CancellationToken ct);

    /// <summary>Removes the code use of a cancelled order (the code can be used again).</summary>
    Task ReleasePromoAsync(long orderId, CancellationToken ct);

    /// <summary>Store owners and factory owners of the order, to tell them.</summary>
    Task<IReadOnlyList<Guid>> PeopleToTellAsync(long orderId, CancellationToken ct);

    Task<string> CustomerNameAsync(Guid userId, CancellationToken ct);

    /// <summary>Owner and type of each store (the official store has no owner).</summary>
    Task<IReadOnlyDictionary<long, (Guid? Owner, StoreType Type)>> StoreOwnersAsync(IReadOnlyList<long> storeIds, CancellationToken ct);

    /// <summary>The order paid by this payment, with items, parcels and payment, tracked; or null.</summary>
    Task<Order?> ByPaymentAsync(long paymentId, CancellationToken ct);
}

/// <summary>Shop settings stored as JSON (ops.Settings).</summary>
public interface IShopSettings
{
    Task<DeliveryFees> DeliveryFeesAsync(CancellationToken ct);
}

/// <summary>The order receipt as a PDF ("every payment has a PDF").</summary>
public interface IShopDocuments
{
    byte[] OrderReceipt(OrderDetailsDto order, string customerName);
}
