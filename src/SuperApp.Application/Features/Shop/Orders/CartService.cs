using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Shop;

namespace SuperApp.Application.Features.Shop.Orders;

/// <summary>The cart and the wishlist (API §4.2). Prices and stock are always read live; codes are checked, not stored.</summary>
public interface ICartService
{
    Task<CartDto> GetAsync(string? promo, bool express, CancellationToken ct);

    Task<CartDto> AddAsync(AddCartItemRequest request, CancellationToken ct);

    Task<CartDto> UpdateAsync(long id, UpdateCartItemRequest request, CancellationToken ct);

    Task<CartDto> RemoveAsync(long id, CancellationToken ct);

    /// <summary>The cart with the code applied; an invalid code → 422 with its reason.</summary>
    Task<CartDto> ApplyPromoAsync(PromoRequest request, CancellationToken ct);

    Task<IReadOnlyList<WishlistItemDto>> WishlistAsync(CancellationToken ct);

    Task<IReadOnlyList<WishlistItemDto>> AddWishAsync(WishlistRequest request, CancellationToken ct);

    Task<IReadOnlyList<WishlistItemDto>> RemoveWishAsync(long id, CancellationToken ct);
}

public sealed class CartService(
    ICartRepository carts,
    IShopSettings settings,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<AddCartItemRequest> addRules,
    IValidator<UpdateCartItemRequest> qtyRules,
    IValidator<PromoRequest> promoRules,
    IValidator<WishlistRequest> wishRules) : ICartService
{
    public const int MaxQty = 20;

    public async Task<CartDto> GetAsync(string? promo, bool express, CancellationToken ct)
    {
        var me = Me();
        var lines = await carts.LinesAsync(me, ct);
        PromoResult? applied = null;
        string? error = null;
        if (!string.IsNullOrWhiteSpace(promo))
        {
            try { applied = await EvaluateAsync(promo, lines, me, ct); }
            catch (BusinessRuleException ex) { error = ex.Message; }
        }
        return await BuildAsync(lines, applied, error, express, ct);
    }

    public async Task<CartDto> AddAsync(AddCartItemRequest request, CancellationToken ct)
    {
        await addRules.ValidateAndThrowAsync(request, ct);
        var me = Me();
        var (size, color) = await CheckOptionsAsync(request, ct);
        var item = await carts.SameItemAsync(me, request.ProductVariantId, request.DesignId, size, color, ct);
        var qty = (item?.Qty ?? 0) + request.Qty;
        if (qty > MaxQty)
        {
            throw new BusinessRuleException($"You can buy up to {MaxQty} of one item.", "qty_limit");
        }
        if (request.ProductVariantId is { } variant && await carts.VariantAvailableAsync(variant, ct) is { } left && left < qty)
        {
            throw new ConflictException(left <= 0 ? "This item is sold out." : $"Only {left} left in stock.", "out_of_stock");
        }
        if (item is null)
        {
            carts.Add(new CartItem { UserId = me, ProductVariantId = request.ProductVariantId, DesignId = request.DesignId, Size = size, Color = color, Qty = qty });
        }
        else
        {
            item.Qty = qty;
        }
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(null, false, ct);
    }

    public async Task<CartDto> UpdateAsync(long id, UpdateCartItemRequest request, CancellationToken ct)
    {
        await qtyRules.ValidateAndThrowAsync(request, ct);
        var me = Me();
        var item = await carts.ItemAsync(me, id, ct) ?? throw new NotFoundException("This item is not in your cart.");
        if (item.ProductVariantId is { } variant && await carts.VariantAvailableAsync(variant, ct) is { } left && left < request.Qty)
        {
            throw new ConflictException(left <= 0 ? "This item is sold out." : $"Only {left} left in stock.", "out_of_stock");
        }
        item.Qty = request.Qty;
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(null, false, ct);
    }

    public async Task<CartDto> RemoveAsync(long id, CancellationToken ct)
    {
        var item = await carts.ItemAsync(Me(), id, ct) ?? throw new NotFoundException("This item is not in your cart.");
        carts.Remove(item);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(null, false, ct);
    }

    public async Task<CartDto> ApplyPromoAsync(PromoRequest request, CancellationToken ct)
    {
        await promoRules.ValidateAndThrowAsync(request, ct);
        var me = Me();
        var lines = await carts.LinesAsync(me, ct);
        return await BuildAsync(lines, await EvaluateAsync(request.Code, lines, me, ct), null, false, ct);
    }

    public async Task<IReadOnlyList<WishlistItemDto>> WishlistAsync(CancellationToken ct) => await carts.WishlistAsync(Me(), ct);

    public async Task<IReadOnlyList<WishlistItemDto>> AddWishAsync(WishlistRequest request, CancellationToken ct)
    {
        await wishRules.ValidateAndThrowAsync(request, ct);
        if (!await carts.AddWishAsync(Me(), request.ProductSlug?.Trim(), request.DesignId, ct))
        {
            throw new NotFoundException("This item isn’t available any more.");
        }
        return await carts.WishlistAsync(Me(), ct);
    }

    public async Task<IReadOnlyList<WishlistItemDto>> RemoveWishAsync(long id, CancellationToken ct)
    {
        if (!await carts.RemoveWishAsync(Me(), id, ct))
        {
            throw new NotFoundException("This item is not in your wishlist.");
        }
        return await carts.WishlistAsync(Me(), ct);
    }

    /// <summary>The code checked against this cart (shared with checkout).</summary>
    public async Task<PromoResult> EvaluateAsync(string code, IReadOnlyList<CartLine> lines, Guid me, CancellationToken ct)
    {
        var promo = await carts.PromoAsync(code.Trim().ToUpperInvariant(), ct);
        var uses = promo is null ? 0 : await carts.PromoUsesAsync(promo.Id, me, ct);
        var handle = promo?.StoreId is { } storeId ? await carts.StoreHandleAsync(storeId, ct) : null;
        return CartPricing.Evaluate(promo, uses, lines, CartPricing.Today(clock), handle);
    }

    private async Task<(string? Size, string? Color)> CheckOptionsAsync(AddCartItemRequest request, CancellationToken ct)
    {
        if (request.ProductVariantId is { } variant)
        {
            _ = await carts.VariantAvailableAsync(variant, ct) ?? throw new NotFoundException("This item isn’t available any more.");
            return (null, null);
        }
        var options = await carts.DesignOptionsAsync(request.DesignId!.Value, ct) ?? throw new NotFoundException("This design isn’t available any more.");
        var size = options.Sizes.FirstOrDefault(s => string.Equals(s, request.Size?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Choose one of the sizes shown.", "bad_size");
        var color = options.Colors.FirstOrDefault(c => string.Equals(c, request.Color?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Choose one of the colours shown.", "bad_color");
        return (size, color);
    }

    private async Task<CartDto> BuildAsync(IReadOnlyList<CartLine> lines, PromoResult? promo, string? error, bool express, CancellationToken ct)
    {
        var fees = await settings.DeliveryFeesAsync(ct);
        var t = CartPricing.Totals(lines, fees, express, promo);
        return new CartDto(
            lines.Select(CartPricing.ToDto).ToList(), lines.Where(l => l.CanBuy).Sum(l => l.Qty), t.Subtotal, t.Savings, t.Discount, t.DeliveryFee,
            t.StandardFee, t.ExpressFee, t.Total, fees.FreeOver, t.FreeGap, promo is null ? null : CartPricing.ToDto(promo with { Discount = t.Discount }), error);
    }

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}

public sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x).Must(x => (x.ProductVariantId is not null) != (x.DesignId is not null)).WithName("productVariantId")
            .WithMessage("Choose a product variant or a design.");
        RuleFor(x => x.Size).NotEmpty().MaximumLength(40).When(x => x.DesignId is not null).WithMessage("Choose a size first.");
        RuleFor(x => x.Color).NotEmpty().MaximumLength(40).When(x => x.DesignId is not null).WithMessage("Choose a colour first.");
        RuleFor(x => x.Qty).InclusiveBetween(1, CartService.MaxQty);
    }
}

public sealed class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator() => RuleFor(x => x.Qty).InclusiveBetween(1, CartService.MaxQty);
}

public sealed class PromoRequestValidator : AbstractValidator<PromoRequest>
{
    public PromoRequestValidator() => RuleFor(x => x.Code).NotEmpty().MaximumLength(30).Matches("^[A-Za-z0-9]+$").WithMessage("Codes have letters and numbers only.");
}

public sealed class WishlistRequestValidator : AbstractValidator<WishlistRequest>
{
    public WishlistRequestValidator()
    {
        RuleFor(x => x).Must(x => string.IsNullOrWhiteSpace(x.ProductSlug) != (x.DesignId is null)).WithName("productSlug").WithMessage("Choose a product or a design.");
        RuleFor(x => x.ProductSlug).MaximumLength(120);
    }
}
