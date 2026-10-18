using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Orders;

/// <summary>Totals of a cart: subtotal, code discount, delivery (standard / express, free over the limit) and total (FR-SHOP-04).</summary>
public sealed record CartTotals(decimal Subtotal, decimal Savings, decimal Discount, decimal StandardFee, decimal ExpressFee, decimal Total, decimal DeliveryFee, decimal FreeGap);

/// <summary>Pure price rules shared by the cart page and checkout, so the customer pays exactly what the cart showed.</summary>
public static class CartPricing
{
    public static CartTotals Totals(IReadOnlyList<CartLine> lines, DeliveryFees fees, bool express, PromoResult? promo)
    {
        var buyable = lines.Where(l => l.CanBuy).ToList();
        var subtotal = buyable.Sum(l => l.LineTotal);
        var savings = buyable.Sum(l => l.CompareAt is { } c && c > l.UnitPrice ? (c - l.UnitPrice) * l.Qty : 0);
        var discount = Math.Min(promo?.Discount ?? 0, subtotal);
        var free = promo?.FreeDelivery == true || subtotal >= fees.FreeOver;
        var standard = buyable.Count == 0 || free ? 0 : fees.Standard;
        var expressFee = buyable.Count == 0 ? 0 : free ? Math.Max(0, fees.Express - fees.Standard) : fees.Express;
        var delivery = express ? expressFee : standard;
        return new CartTotals(subtotal, savings, discount, standard, expressFee, Math.Max(0, subtotal - discount + delivery), delivery, Math.Max(0, fees.FreeOver - subtotal));
    }

    /// <summary>
    /// Checks a code for this cart. Super App codes discount the whole cart; a store's code only that store's items.
    /// Invalid → 422 with code invalid_code, code_expired, code_used_up, min_spend or code_not_for_cart.
    /// </summary>
    public static PromoResult Evaluate(PromoCode? code, int usesByCustomer, IReadOnlyList<CartLine> lines, DateOnly today, string? storeHandle)
    {
        if (code is null || code.Status != CodeStatus.Active || code.Scope == CodeScope.Bookings)
        {
            throw new BusinessRuleException("That code isn’t valid.", "invalid_code");
        }
        if (today < code.StartsOn || (code.EndsOn is { } end && today > end))
        {
            throw new BusinessRuleException("This code has expired.", "code_expired");
        }
        if ((code.MaxUses is { } max && code.UsesCount >= max) || (code.PerCustomer is { } per && usesByCustomer >= per))
        {
            throw new BusinessRuleException("This code has been used up.", "code_used_up");
        }
        var eligible = lines.Where(l => l.CanBuy && (code.StoreId is null || l.StoreId == code.StoreId)).Sum(l => l.LineTotal);
        if (eligible <= 0)
        {
            throw new BusinessRuleException("This code is for another store’s items.", "code_not_for_cart");
        }
        if (eligible < code.MinSpend)
        {
            throw new BusinessRuleException($"Spend at least {code.MinSpend:0} EGP to use this code.", "min_spend");
        }
        var discount = code.DiscountType switch
        {
            DiscountType.Percent => Math.Round(eligible * code.Value / 100, 0, MidpointRounding.AwayFromZero),
            DiscountType.Fixed => Math.Min(code.Value, eligible),
            _ => 0,
        };
        return new PromoResult(code.Id, code.Code, code.Label, code.Funding, code.StoreId, storeHandle, discount, code.DiscountType == DiscountType.FreeDelivery);
    }

    public static CartLineDto ToDto(CartLine l) => new(
        l.Id, l.Kind == OrderItemKind.PrintedDesign ? "design" : "product", l.ItemId, l.VariantId, l.DesignId, l.Title, l.Image,
        new StoreRefDto(l.StoreHandle, l.StoreName, l.StoreType.ToString()), l.Size, l.Color, l.Qty, l.UnitPrice, l.CompareAt,
        l.Kind == OrderItemKind.StoreProduct ? l.Available : null, l.CanBuy, l.Problem);

    public static PromoDto ToDto(PromoResult p) =>
        new(p.Code, p.Label, p.Funding == CodeFunding.SuperAppFunded ? "SuperApp" : "Store", p.Discount, p.FreeDelivery, p.StoreHandle);

    /// <summary>Today in Cairo (codes start and end on Cairo dates).</summary>
    public static DateOnly Today(IClock clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo")));
}
