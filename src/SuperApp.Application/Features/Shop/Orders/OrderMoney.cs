using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Orders;

/// <summary>
/// Who earns what on each order item (FR-SHOP-08, FR-POD-04). Amounts on the item are line totals (× qty);
/// <c>BaseAmount</c> is per unit. Phase 5 turns these into ledger entries when the item is delivered.
/// <list type="bullet">
/// <item>Store product: commission = line × rate (official store 0, partner deal, seller level, else the default); the store earns the rest minus its own code's share.</item>
/// <item>Printed design: the seller earns (price − base) × qty; Super App keeps (base − factory cost) × qty; the factory is paid its cost.</item>
/// </list>
/// </summary>
public static class OrderMoney
{
    public static OrderItem Item(CartLine line, decimal defaultRate, decimal storeCodeShare)
    {
        var item = new OrderItem
        {
            Kind = line.Kind, StoreId = line.StoreId, ProductVariantId = line.VariantId, DesignId = line.DesignId, Title = Cut(line.Title, 150),
            Size = Cut(line.Size, 40), Color = Cut(line.Color, 40), Qty = line.Qty, UnitPrice = line.UnitPrice, Status = OrderItemStatus.Received,
        };
        if (line.Printed is { } p)
        {
            item.BaseAmount = p.BaseUnit;
            item.CommissionAmount = Math.Max(0, (p.BaseUnit - p.FactoryCost) * line.Qty);
            item.SellerEarning = Math.Max(0, ((line.UnitPrice - p.BaseUnit) * line.Qty) - storeCodeShare);
            return item;
        }
        var rate = line.StoreType == StoreType.Official ? 0 : line.CommissionRate ?? defaultRate;
        item.CommissionAmount = Math.Round(line.LineTotal * rate, 2);
        item.SellerEarning = Math.Max(0, line.LineTotal - item.CommissionAmount - storeCodeShare);
        return item;
    }

    /// <summary>A store's own code is paid by that store: its discount is shared over the store's lines by value.</summary>
    public static decimal StoreShare(CartLine line, IReadOnlyList<CartLine> lines, PromoResult? promo, decimal discount)
    {
        if (promo is not { Funding: CodeFunding.OwnerFunded, StoreId: { } storeId } || line.StoreId != storeId || discount <= 0)
        {
            return 0;
        }
        var eligible = lines.Where(l => l.StoreId == storeId).Sum(l => l.LineTotal);
        return eligible <= 0 ? 0 : Math.Round(discount * line.LineTotal / eligible, 2);
    }

    private static string Cut(string value, int max) => value.Length > max ? value[..max] : value;
}
