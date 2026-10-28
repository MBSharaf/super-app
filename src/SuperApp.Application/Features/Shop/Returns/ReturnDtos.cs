using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Entities.Shop;

namespace SuperApp.Application.Features.Shop.Returns;

/// <summary>An item that can still be returned: how many, until when, and what is allowed (Refund, Exchange for store products; Reprint, Refund for printed items).</summary>
public sealed record ReturnableItemDto(long OrderItemId, string Kind, string Title, string Size, string Color, int QtyLeft, decimal UnitPrice, DateTime Until, IReadOnlyList<string> Actions, PhotoDto? Image);

public sealed record ReturnItemRequest(long OrderItemId, int Qty, string Action, string? NewSize, string? NewColor);

/// <summary>Reason: FaultyOrDamaged, WrongItemOrPrint, NotAsDescribed (WrongSize and ChangedMyMind are refused: returns are for an item with a problem).</summary>
public sealed record ReturnRequestDto(string OrderCode, IReadOnlyList<ReturnItemRequest> Items, string Reason, string? Detail);

public sealed record ReturnLineDto(long OrderItemId, string Title, string Size, string Color, int Qty, string Action, string? NewSize, string? NewColor, PhotoDto? Image);

/// <summary>A return. Status: Requested, Approved, Received, Refunded, Exchanged, Reprinting, Rejected. DecidedBy: the store or "Super App".</summary>
public sealed record ReturnDto(string Code, string OrderCode, string Reason, string? Detail, string Status, string DecidedBy, string? RejectReason, decimal RefundAmount,
    bool CustomerPaysDelivery, string? PickupTracking, DateTime CreatedAt, IReadOnlyList<ReturnLineDto> Lines, string? CustomerName = null, string? City = null);

public sealed record RejectReturnRequest(string Reason);

/// <summary>Who decides a return: a store (its own products) or Super App (official store and printed items).</summary>
public sealed record ReturnScope(long? StoreId, string Name, Domain.Enums.AuditSystem System);

/// <summary>Returns in the database.</summary>
public interface IReturnRepository
{
    /// <summary>The customer's order with items (tracked), or null.</summary>
    Task<Order?> OrderAsync(Guid customerId, string code, CancellationToken ct);

    /// <summary>Pieces of each order item already in a return that was not rejected.</summary>
    Task<IReadOnlyDictionary<long, int>> ReturnedQtyAsync(long orderId, CancellationToken ct);

    Task<IReadOnlyList<ReturnableItemDto>> ItemsAsync(long orderId, CancellationToken ct);

    Task<string> NewCodeAsync(CancellationToken ct);

    void Add(ReturnRequest request);

    Task<IReadOnlyList<ReturnDto>> MineAsync(Guid customerId, CancellationToken ct);

    Task<ReturnDto?> DtoAsync(string code, CancellationToken ct);

    /// <summary>Returns to decide for a store (its products) or for Super App (storeId null: official store and printed items).</summary>
    Task<IReadOnlyList<ReturnDto>> ForDeciderAsync(long? storeId, string? status, CancellationToken ct);

    /// <summary>The return with items and the order (items, parcels), tracked, when this decider owns it.</summary>
    Task<ReturnRequest?> ForDecisionAsync(long? storeId, string code, CancellationToken ct);

    Task<IReadOnlyList<Domain.Entities.Factories.Job>> JobsAsync(IReadOnlyList<long> orderItemIds, CancellationToken ct);

    /// <summary>A variant of the same product in another size / colour (exchanges).</summary>
    Task<long?> SiblingVariantAsync(long variantId, string? size, string? color, CancellationToken ct);

    Task<Guid?> StoreOwnerAsync(long storeId, CancellationToken ct);

    /// <summary>Takes pieces from a variant when there are enough (false otherwise).</summary>
    Task<bool> TakeStockAsync(long variantId, int qty, CancellationToken ct);
}
