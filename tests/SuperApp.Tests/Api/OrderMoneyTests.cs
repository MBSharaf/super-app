using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Payments;

namespace SuperApp.Tests.Api;

/// <summary>
/// Store order money (owner's decisions, 2026-10-27): paid online, 5% commission, the store's earning held until 24 hours
/// after delivery, refunds to the customer's wallet, unpaid orders called off after 15 minutes.
/// </summary>
[Trait("Category", "Database")]
public sealed class OrderMoneyTests(DemoApiFactory api) : IClassFixture<DemoApiFactory>
{
    private readonly ShopKit kit = new(api);
    private readonly PartnerKit partners = new(api);

    private static JsonElement Wallet(JsonElement wallets, string type) => wallets.EnumerateArray().Single(w => w.GetProperty("type").GetString() == type);

    /// <summary>A partner store with one approved product at 300 EGP; a customer who put one in the cart.</summary>
    private async Task<(ApiClient Partner, ApiClient Customer, long Variant)> CartAsync()
    {
        var (partner, _) = await partners.PersonAsync(partner: true);
        await partner.GetAsync("/api/v1/partner/store");
        var product = (await partner.PostAsync("/api/v1/partner/store/products", new
        {
            name = "Grip Socks", categoryKey = "training", price = 300m, lowStockAt = 1, status = "Published", specs = Array.Empty<object>(),
            variants = new[] { new { id = (long?)null, size = "One size", colorName = "White", colorHex = (string?)null, sku = (string?)null, stock = 10, priceOverride = (decimal?)null } },
        })).Body.Data();
        var id = product.GetProperty("id").GetInt64();
        await partner.SendFormAsync(HttpMethod.Post, $"/api/v1/partner/store/products/{id}/images", new { }, ("photos", "s.png", ShopKit.Png()));
        await kit.ApproveAsync(id);
        var (customer, _) = await kit.PersonAsync();
        var variant = product.GetProperty("variants")[0].GetProperty("id").GetInt64();
        await customer.PostAsync("/api/v1/cart/items", new { productVariantId = variant, qty = 1 });
        return (partner, customer, variant);
    }

    [Fact]
    public async Task A_paid_order_holds_the_stores_earning_until_24_hours_after_delivery()
    {
        var (partner, customer, _) = await CartAsync();
        var placed = (await ShopKit.CheckoutAsync(customer)).Body.Data();
        placed.GetProperty("status").GetString().ShouldBe("Paid");
        var code = placed.GetProperty("code").GetString()!;

        var held = Wallet((await partner.GetAsync("/api/v1/wallet")).Body.Data(), "PartnerStore");
        held.GetProperty("pending").GetDecimal().ShouldBe(300 - 15);   // 5% commission
        held.GetProperty("available").GetDecimal().ShouldBe(0);
        (await customer.GetAsync("/api/v1/receipts")).Body.Data()[0].GetProperty("amount").GetDecimal().ShouldBe(placed.GetProperty("total").GetDecimal());

        await kit.DeliverAsync(partner, code);
        var until = await api.DbAsync(db => db.Set<LedgerEntry>().Where(e => e.RefType == "OrderItem" && e.Kind == LedgerKind.StoreEarning)
            .Where(e => db.Set<OrderItem>().Any(i => i.Id == e.RefId && i.Order.Code == code)).Select(e => e.AvailableAt).SingleAsync());
        until.ShouldNotBeNull();
        until!.Value.ShouldBeInRange(DateTime.UtcNow.AddHours(23), DateTime.UtcNow.AddHours(25));

        var job = api.Services.GetRequiredService<MoneyReleaseJob>();
        await job.RunOnceAsync(CancellationToken.None);
        Wallet((await partner.GetAsync("/api/v1/wallet")).Body.Data(), "PartnerStore").GetProperty("available").GetDecimal().ShouldBe(0);   // still inside the return window

        await api.DbAsync(db => db.Set<LedgerEntry>().Where(e => e.RefType == "OrderItem" && e.AvailableAt == until)
            .ExecuteUpdateAsync(u => u.SetProperty(e => e.AvailableAt, DateTime.UtcNow.AddMinutes(-1))));
        await job.RunOnceAsync(CancellationToken.None);
        var released = Wallet((await partner.GetAsync("/api/v1/wallet")).Body.Data(), "PartnerStore");
        released.GetProperty("available").GetDecimal().ShouldBe(300 - 15);
        released.GetProperty("pending").GetDecimal().ShouldBe(0);
        // The store wallet goes through the payouts flow (285 EGP is under the 500 EGP minimum, so the account was found)
        (await partner.PostAsync("/api/v1/wallet/payouts", new { walletType = "PartnerStore", amount = 285m })).Body.Code().ShouldBe("under_minimum");
        (await customer.PostAsync("/api/v1/wallet/payouts", new { walletType = "PartnerStore", amount = 600m })).Body.Code().ShouldBe("no_payout_account");
    }

    [Fact]
    public async Task Cancelling_a_paid_order_refunds_everything_to_the_wallet_and_takes_the_earning_back()
    {
        var (partner, customer, variant) = await CartAsync();
        var placed = (await ShopKit.CheckoutAsync(customer)).Body.Data();
        var code = placed.GetProperty("code").GetString()!;
        (await customer.PostAsync($"/api/v1/orders/{code}/cancel")).Status.ShouldBe(HttpStatusCode.OK);

        Wallet((await customer.GetAsync("/api/v1/wallet")).Body.Data(), "Customer").GetProperty("available").GetDecimal()
            .ShouldBe(placed.GetProperty("total").GetDecimal());   // the items and the delivery fee
        Wallet((await partner.GetAsync("/api/v1/wallet")).Body.Data(), "PartnerStore").GetProperty("pending").GetDecimal().ShouldBe(0);
        (await customer.GetAsync("/api/v1/receipts")).Body.Data().GetArrayLength().ShouldBe(2);

        // The refund pays for the same order again from the wallet
        await customer.PostAsync("/api/v1/cart/items", new { productVariantId = variant, qty = 1 });
        var paid = await ShopKit.CheckoutAsync(customer, payment: "wallet");
        paid.Status.ShouldBe(HttpStatusCode.Created, paid.Body.ToString());
        paid.Body.Data().GetProperty("status").GetString().ShouldBe("Paid");
    }

    [Fact]
    public async Task An_order_not_paid_within_15_minutes_is_called_off_and_the_stock_comes_back()
    {
        var (_, customer, variant) = await CartAsync();
        var code = (await ShopKit.CheckoutAsync(customer)).Body.Data().GetProperty("code").GetString()!;
        await api.DbAsync(db => db.Set<Order>().Where(o => o.Code == code)
            .ExecuteUpdateAsync(u => u.SetProperty(o => o.Status, OrderStatus.Placed).SetProperty(o => o.CreatedAt, DateTime.UtcNow.AddMinutes(-20))));

        (await api.Services.GetRequiredService<AwaitingPaymentExpiry>().RunOnceAsync(CancellationToken.None)).ShouldBeGreaterThanOrEqualTo(1);
        (await customer.GetAsync($"/api/v1/orders/{code}")).Body.Data().GetProperty("status").GetString().ShouldBe("Cancelled");
        (await api.DbAsync(db => db.Set<ProductVariant>().Where(v => v.Id == variant).Select(v => v.Stock).SingleAsync())).ShouldBe(10);
    }
}
