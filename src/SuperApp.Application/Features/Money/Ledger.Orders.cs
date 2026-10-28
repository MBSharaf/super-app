using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Money;

/// <summary>
/// One order item's money: the store's earning (held in its wallet; none for the official store), Super App's commission,
/// and what the customer paid for it after discounts (used for refunds).
/// </summary>
public sealed record OrderShare(long ItemId, Guid? OwnerUserId, WalletType? Wallet, decimal Earning, decimal Commission, decimal CustomerAmount);

/// <summary>
/// Store orders in the ledger (owner's decisions, 2026-10-27): a paid order holds each store's earning in its wallet with no
/// release date; delivery sets the date (24 hours later — the return window) and the release job moves it to Available.
/// Super App's revenue wallet gets the commission, the official store's sales and the delivery fee, less what Super App
/// pays of a discount. A refund goes to the customer's Super App wallet and takes each share back.
/// </summary>
public sealed partial class Ledger
{
    public const string OrderItemRef = "OrderItem";
    public static readonly TimeSpan ReturnWindow = TimeSpan.FromHours(24);

    public async Task OrderPaidAsync(Order order, IReadOnlyList<OrderShare> shares, CancellationToken ct)
    {
        var group = Guid.NewGuid();
        var revenue = await store.WalletAsync(null, WalletType.PlatformRevenue, ct);
        decimal held = 0, official = 0;
        foreach (var s in shares)
        {
            if (s.OwnerUserId is { } owner && s.Wallet is { } type)
            {
                PostRaw(await store.WalletAsync(owner, type, ct), s.Earning, LedgerBucket.Pending, LedgerKind.StoreEarning, OrderItemRef, s.ItemId, null, group);
                held += s.Earning;
            }
            else
            {
                official += s.Earning;
            }
        }
        var commission = shares.Sum(s => s.Commission);
        PostRaw(revenue, commission, LedgerBucket.Available, LedgerKind.Commission, "Order", order.Id, null, group);
        PostRaw(revenue, official, LedgerBucket.Available, LedgerKind.StoreEarning, "Order", order.Id, null, group);
        PostRaw(revenue, order.DeliveryFee, LedgerBucket.Available, LedgerKind.ServiceFee, "Order", order.Id, null, group);
        // What is left is the part of a discount Super App pays (never more than the order's own numbers)
        PostRaw(revenue, order.Total - held - commission - official - order.DeliveryFee, LedgerBucket.Available, LedgerKind.Voucher, "Order", order.Id, null, group);
        store.Add(new Receipt { Number = await store.NewReceiptNumberAsync(ReceiptKind.Payment, ct), UserId = order.CustomerId, Kind = ReceiptKind.Payment,
            Amount = order.Total, RefType = "Order", RefId = order.Id });
    }

    public async Task OrderRefundAsync(Order order, IReadOnlyList<OrderShare> items, decimal deliveryFee, CancellationToken ct)
    {
        var amount = items.Sum(i => i.CustomerAmount) + deliveryFee;
        if (amount <= 0)
        {
            return;
        }
        var group = Guid.NewGuid();
        PostRaw(await store.WalletAsync(order.CustomerId, WalletType.Customer, ct), amount, LedgerBucket.Available, LedgerKind.Refund, "Order", order.Id, null, group);
        decimal fromStores = 0;
        foreach (var s in items.Where(i => i.OwnerUserId is not null && i.Wallet is not null && i.Earning > 0))
        {
            PostRaw(await store.WalletAsync(s.OwnerUserId, s.Wallet!.Value, ct), -s.Earning, LedgerBucket.Pending, LedgerKind.Deduction, OrderItemRef, s.ItemId, null, group);
            fromStores += s.Earning;
        }
        PostRaw(await store.WalletAsync(null, WalletType.PlatformRevenue, ct), -(amount - fromStores), LedgerBucket.Available, LedgerKind.Refund, "Order", order.Id, null, group);
        store.Add(new Receipt { Number = await store.NewReceiptNumberAsync(ReceiptKind.Refund, ct), UserId = order.CustomerId, Kind = ReceiptKind.Refund,
            Amount = amount, RefType = "Order", RefId = order.Id });
    }

    /// <summary>Delivered items: their held earning becomes available after the return window (null = keep holding, a return is open).</summary>
    public Task HoldItemsUntilAsync(IReadOnlyList<long> itemIds, DateTime? until, CancellationToken ct) =>
        itemIds.Count == 0 ? Task.CompletedTask : store.HoldUntilAsync(OrderItemRef, itemIds, until, ct);
}
