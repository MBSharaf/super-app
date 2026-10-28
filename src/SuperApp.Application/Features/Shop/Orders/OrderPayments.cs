using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Features.Money;
using SuperApp.Application.Features.Payments;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Orders;

/// <summary>
/// An order's money (owner's decisions, 2026-10-27: online payment only, 5% commission, returns within 24 hours): when the
/// payment is confirmed the order becomes Paid, the ledger holds each store's earning and everyone concerned is told;
/// cancelled or returned items give the customer their money back to the Super App wallet.
/// </summary>
public sealed class OrderPayments(IOrderRepository orders, ILedger ledger, IUnitOfWork unitOfWork, OrderNotices notices)
{
    public async Task PaidAsync(Order order, CancellationToken ct)
    {
        if (order.Status != OrderStatus.Placed)
        {
            return;
        }
        order.Status = OrderStatus.Paid;
        var shares = await SharesAsync(order, order.Items, ct);
        await ledger.OrderPaidAsync(order, shares, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await notices.PaidAsync(order, shares, ct);
    }

    /// <summary>Money back for these items (and the delivery fee when the whole order is called off). Nothing when it was never paid.</summary>
    public async Task RefundAsync(Order order, IReadOnlyList<OrderItem> items, bool deliveryFee, bool wasPaid, CancellationToken ct)
    {
        if (!wasPaid || (items.Count == 0 && !deliveryFee))
        {
            return;
        }
        await ledger.OrderRefundAsync(order, await SharesAsync(order, items, ct), deliveryFee ? order.DeliveryFee : 0, ct);
        if (order.Status == OrderStatus.Cancelled && order.Payment is { } payment)
        {
            payment.Status = PaymentStatus.Refunded;
        }
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Paid and not yet closed: something the customer paid for can be refunded.</summary>
    public static bool IsPaid(OrderStatus status) => status is OrderStatus.Paid or OrderStatus.InProgress or OrderStatus.Completed;

    public async Task<IReadOnlyList<OrderShare>> SharesAsync(Order order, IEnumerable<OrderItem> items, CancellationToken ct)
    {
        var stores = await orders.StoreOwnersAsync(order.Items.Select(i => i.StoreId).Distinct().ToList(), ct);
        var storeCodes = order.Items.Sum(StoreCodeShare);
        var platformDiscount = Math.Max(0, order.DiscountAmount - storeCodes);
        return items.Select(i =>
        {
            var line = i.UnitPrice * i.Qty;
            var paid = Math.Round(line - StoreCodeShare(i) - (order.Subtotal > 0 ? platformDiscount * line / order.Subtotal : 0), 2);
            var (owner, type) = stores.GetValueOrDefault(i.StoreId);
            var wallet = type switch { StoreType.Seller => WalletType.Seller, StoreType.PitchPartner => (WalletType?)WalletType.PartnerStore, _ => null };
            return new OrderShare(i.Id, wallet is null ? null : owner, wallet, i.SellerEarning, i.CommissionAmount, paid);
        }).ToList();
    }

    /// <summary>What a store's own code took off this item (the store pays it): the line less commission and earning.</summary>
    private static decimal StoreCodeShare(OrderItem i) =>
        i.Kind == OrderItemKind.StoreProduct ? Math.Max(0, (i.UnitPrice * i.Qty) - i.CommissionAmount - i.SellerEarning) : 0;
}

/// <summary>The gateway confirmed (or refused) an order's payment.</summary>
public sealed class OrderPaymentListener(IOrderRepository orders, OrderPayments money, INotifier notifier) : IPaymentListener
{
    public PaymentPurpose Purpose => PaymentPurpose.ShopOrder;

    public async Task PaidAsync(Payment payment, CancellationToken ct)
    {
        if (await orders.ByPaymentAsync(payment.Id, ct) is { } order)
        {
            await money.PaidAsync(order, ct);
        }
    }

    public async Task FailedAsync(Payment payment, CancellationToken ct)
    {
        if (await orders.ByPaymentAsync(payment.Id, ct) is { Status: OrderStatus.Placed } order)
        {
            await notifier.NotifyUserAsync(order.CustomerId, new NotificationMessage(NotificationCategory.Orders, "Payment did not go through",
                $"Order {order.Code}: try again within 15 minutes to keep the items.", $"#/order/{order.Code}", NotificationTone.Warning), ct);
        }
    }
}
