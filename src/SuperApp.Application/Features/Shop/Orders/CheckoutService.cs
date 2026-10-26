using System.Text.Json;
using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Validation;
using SuperApp.Application.Features.Bookings;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Orders;

/// <summary>
/// Checkout (API آ§4.2, FR-SHOP-04..06, FR-POD-10): checks every line again, prices the cart exactly as shown, takes the
/// stock, creates the order with one parcel per store and one factory job per printed item, and empties the cart.
/// Payment seam: until Phase 5 online and wallet payments are simulated (the order is Paid at once) and cash on
/// delivery leaves it Placed; Phase 5 swaps <see cref="PayAsync"/> for the payment gateway session.
/// </summary>
public interface ICheckoutService
{
    Task<CheckoutResultDto> CheckoutAsync(CheckoutRequest request, string idempotencyKey, CancellationToken ct);
}

public sealed class CheckoutService(
    ICartRepository carts,
    IOrderRepository orders,
    CartService cart,
    IShopSettings shopSettings,
    ISettingsReader settings,
    IFeatureFlags flags,
    IMyOrdersService myOrders,
    OrderNotices notices,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<CheckoutRequest> rules) : ICheckoutService
{
    public async Task<CheckoutResultDto> CheckoutAsync(CheckoutRequest request, string idempotencyKey, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var me = currentUser.UserId ?? throw new UnauthorizedException();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 64)
        {
            throw new BadRequestException("Send an Idempotency-Key header (up to 64 characters) with each checkout.", "idempotency_key");
        }
        if (await orders.CodeByKeyAsync(me, idempotencyKey, ct) is { } placed)
        {
            return await ResultAsync(placed, ct);
        }
        await flags.EnsureOnAsync(KillSwitches.Shop, ct);
        if ((await orders.CustomerRulesAsync(me, ct)).Status != UserStatus.Active)
        {
            throw new ForbiddenException("Your account can’t place orders right now. Contact support.", "account_blocked");
        }
        var lines = await CheckLinesAsync(me, ct);
        var payment = PaymentOf(request.PaymentMethod);
        var factory = await CheckPrintingAsync(lines, payment, me, ct);
        var promo = string.IsNullOrWhiteSpace(request.PromoCode) ? null : await cart.EvaluateAsync(request.PromoCode, lines, me, ct);
        var (address, phone) = await AddressAsync(request, me, ct);
        var order = await BuildOrderAsync(lines, request, promo, payment, address, phone, me, ct);
        order.IdempotencyKey = idempotencyKey;
        var jobs = await JobsAsync(order, factory, ct);
        await orders.PlaceAsync(order, jobs, StockOf(order), PromoUse(order, promo, me), lines.Select(l => l.Id).ToList(), ct);
        await notices.PlacedAsync(order, lines, factory?.OwnerUserId, ct);
        return await ResultAsync(order.Code, ct);
    }

    private async Task<CheckoutResultDto> ResultAsync(string code, CancellationToken ct)
    {
        var details = await myOrders.GetAsync(code, ct);
        return new CheckoutResultDto(details.Code, details.Status, details.PaymentMethod, details.PaymentMethod != nameof(OrderPaymentMethod.CashOnDelivery), details.Total, details);
    }

    private async Task<IReadOnlyList<CartLine>> CheckLinesAsync(Guid me, CancellationToken ct)
    {
        var lines = await carts.LinesAsync(me, ct);
        if (lines.Count == 0)
        {
            throw new BusinessRuleException("Your cart is empty.", "empty_cart");
        }
        if (lines.FirstOrDefault(l => !l.CanBuy) is { } bad)
        {
            throw new ConflictException($"â€œ{bad.Title}â€‌: {bad.Problem} Update your cart and try again.", "item_unavailable");
        }
        return lines;
    }

    /// <summary>Printed items need an open factory, the print-on-demand switch on, and online payment for customers who refused deliveries.</summary>
    private async Task<(long Id, Guid OwnerUserId, short SlaHours)?> CheckPrintingAsync(IReadOnlyList<CartLine> lines, OrderPaymentMethod payment, Guid me, CancellationToken ct)
    {
        if (!lines.Any(l => l.Printed is not null))
        {
            return null;
        }
        await flags.EnsureOnAsync(KillSwitches.Pod, ct);
        var factory = await orders.AcceptingFactoryAsync(ct)
            ?? throw new ConflictException("Printing is paused for a few days. Remove the printed items or try again later.", "printing_paused");
        var limit = await settings.DecimalAsync("pod.refuseLimit", 2, ct);
        var (rule, refusals, _) = await orders.CustomerRulesAsync(me, ct);
        var mustPrepay = rule == PrepayRule.Always || (rule == PrepayRule.Automatic && refusals >= limit);
        if (payment == OrderPaymentMethod.CashOnDelivery && mustPrepay)
        {
            throw new BusinessRuleException("Printed-to-order items need online payment on this account because of earlier refused deliveries. Pay by card or wallet.", "prepay_required");
        }
        return factory;
    }

    private async Task<(string Json, string Phone)> AddressAsync(CheckoutRequest request, Guid me, CancellationToken ct)
    {
        if (request.AddressId is { } id)
        {
            var saved = await orders.AddressAsync(me, id, ct) ?? throw new NotFoundException("This address is not in your account.");
            return (saved.Json, string.IsNullOrWhiteSpace(request.Phone) ? saved.Phone : EgyptianPhone.Normalize(request.Phone) ?? request.Phone);
        }
        var a = request.Address!;
        var json = JsonSerializer.Serialize(new { label = a.Label?.Trim(), line1 = a.Line1.Trim(), line2 = a.Line2?.Trim(), city = a.City.Trim(), areaId = a.AreaId });
        return (json, EgyptianPhone.Normalize(request.Phone) ?? request.Phone.Trim());
    }

    private async Task<Order> BuildOrderAsync(IReadOnlyList<CartLine> lines, CheckoutRequest request, PromoResult? promo, OrderPaymentMethod payment,
        string address, string phone, Guid me, CancellationToken ct)
    {
        var express = request.DeliverySpeed == "express";
        var totals = CartPricing.Totals(lines, await shopSettings.DeliveryFeesAsync(ct), express, promo);
        var vat = await settings.DecimalAsync("vat.rate", 0.14m, ct);
        var defaultRate = await settings.DecimalAsync("commission.store", 0.12m, ct);
        var order = new Order
        {
            Code = await orders.NewCodeAsync(ct), CustomerId = me, AddressJson = address, Phone = phone, Subtotal = totals.Subtotal,
            DiscountAmount = totals.Discount, DeliveryFee = totals.DeliveryFee, Total = totals.Total, VatAmount = Math.Round(totals.Total * vat / (1 + vat), 2),
            PromoCodeId = promo?.PromoId, PaymentMethod = payment, DeliverySpeed = express ? DeliverySpeed.Express : DeliverySpeed.Standard,
        };
        foreach (var line in lines)
        {
            order.Items.Add(OrderMoney.Item(line, defaultRate, OrderMoney.StoreShare(line, lines, promo, totals.Discount)));
        }
        foreach (var storeId in lines.Where(l => l.Printed is null).Select(l => l.StoreId).Distinct())
        {
            order.Fulfilments.Add(new Fulfilment { StoreId = storeId, Status = FulfilmentStatus.New });
        }
        order.Status = await PayAsync(order, ct);
        return order;
    }

    /// <summary>Phase 5 seam: start the gateway session here. Until then online / wallet are simulated as paid, cash waits for delivery.</summary>
    private static Task<OrderStatus> PayAsync(Order order, CancellationToken ct)
    {
        _ = ct;
        return Task.FromResult(order.PaymentMethod == OrderPaymentMethod.CashOnDelivery ? OrderStatus.Placed : OrderStatus.Paid);
    }

    private async Task<IReadOnlyList<Job>> JobsAsync(Order order, (long Id, Guid OwnerUserId, short SlaHours)? factory, CancellationToken ct)
    {
        var jobs = new List<Job>();
        foreach (var item in order.Items.Where(i => i.Kind == OrderItemKind.PrintedDesign))
        {
            jobs.Add(new Job { Code = await orders.NewJobCodeAsync(ct), OrderItem = item, FactoryId = factory!.Value.Id, Stage = JobStage.New, DueAt = clock.UtcNow.AddHours(factory.Value.SlaHours) });
        }
        return jobs;
    }

    private static List<(long, int)> StockOf(Order order) =>
        order.Items.Where(i => i.ProductVariantId is not null).GroupBy(i => i.ProductVariantId!.Value).Select(g => (g.Key, g.Sum(i => i.Qty))).ToList();

    private static PromoCodeUse? PromoUse(Order order, PromoResult? promo, Guid me) =>
        promo is null || (order.DiscountAmount <= 0 && !promo.FreeDelivery) ? null
            : new PromoCodeUse { PromoCodeId = promo.PromoId, UserId = me, Order = order, DiscountAmount = Math.Max(order.DiscountAmount, 0.01m), RevenueAmount = order.Subtotal - order.DiscountAmount };

    private static OrderPaymentMethod PaymentOf(string method) => method switch
    {
        "cod" => OrderPaymentMethod.CashOnDelivery,
        "wallet" => OrderPaymentMethod.Wallet,
        _ => OrderPaymentMethod.Online,
    };
}

public sealed class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(x => x).Must(x => x.AddressId is not null || x.Address is not null).WithName("address").WithMessage("Add a delivery address.");
        RuleFor(x => x.Address!.Line1).NotEmpty().Length(5, 200).When(x => x.Address is not null).OverridePropertyName("address.line1");
        RuleFor(x => x.Address!.Line2).MaximumLength(200).When(x => x.Address is not null).OverridePropertyName("address.line2");
        RuleFor(x => x.Address!.City).NotEmpty().MaximumLength(60).When(x => x.Address is not null).OverridePropertyName("address.city");
        RuleFor(x => x.Address!.Label).MaximumLength(40).When(x => x.Address is not null).OverridePropertyName("address.label");
        RuleFor(x => x.Phone).NotEmpty().When(x => x.AddressId is null).WithMessage("Add a phone number for the courier.");
        RuleFor(x => x.Phone).EgyptianMobile().When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.DeliverySpeed).Must(s => s is "standard" or "express").WithMessage("Choose standard or express delivery.");
        RuleFor(x => x.PaymentMethod).Must(s => s is "online" or "cod" or "wallet").WithMessage("Choose card, wallet or cash on delivery.");
        RuleFor(x => x.PromoCode).MaximumLength(30);
    }
}
