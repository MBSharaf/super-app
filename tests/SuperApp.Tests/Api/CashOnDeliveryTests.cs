using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>The store paid by cash on delivery only (setting Features:ShopPayment = cod).</summary>
public sealed class CodApiFactory : AccountsApiFactory
{
    protected override bool WithDemoData => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Features:ShopPayment", "cod");
    }
}

/// <summary>
/// Owner's rule (2026-11-23): until the payment gateway is ready, store orders are paid in cash on delivery. The order goes
/// to the store at once; the store's earning is held until 24 hours after delivery; the receipt is made when the courier
/// collects the money; a cancelled or refused order is taken back without any refund (nothing was paid).
/// </summary>
[Trait("Category", "Database")]
public sealed class CashOnDeliveryTests(CodApiFactory api) : IClassFixture<CodApiFactory>
{
    private readonly ShopKit kit = new(api);
    private readonly PartnerKit partners = new(api);

    private static JsonElement Wallet(JsonElement wallets, string type) => wallets.EnumerateArray().Single(w => w.GetProperty("type").GetString() == type);

    private async Task<(ApiClient Partner, ApiClient Customer)> CartAsync()
    {
        var (partner, _) = await partners.PersonAsync(partner: true);
        await partner.GetAsync("/api/v1/partner/store");
        var product = (await partner.PostAsync("/api/v1/partner/store/products", new
        {
            name = "Shin Guards", categoryKey = "training", price = 300m, lowStockAt = 1, status = "Published", specs = Array.Empty<object>(),
            variants = new[] { new { id = (long?)null, size = "One size", colorName = "Black", colorHex = (string?)null, sku = (string?)null, stock = 10, priceOverride = (decimal?)null } },
        })).Body.Data();
        var id = product.GetProperty("id").GetInt64();
        await partner.SendFormAsync(HttpMethod.Post, $"/api/v1/partner/store/products/{id}/images", new { }, ("photos", "s.png", ShopKit.Png()));
        await kit.ApproveAsync(id);
        var (customer, _) = await kit.PersonAsync();
        await customer.PostAsync("/api/v1/cart/items", new { productVariantId = product.GetProperty("variants")[0].GetProperty("id").GetInt64(), qty = 1 });
        return (partner, customer);
    }

    [Fact]
    public async Task A_cash_on_delivery_order_goes_to_the_store_and_the_receipt_comes_when_the_courier_collects()
    {
        var (partner, customer) = await CartAsync();
        var card = await ShopKit.CheckoutAsync(customer);
        card.Status.ShouldBe(HttpStatusCode.Locked);
        card.Body.Code().ShouldBe("cod_only");

        var placed = await ShopKit.CheckoutAsync(customer, payment: "cod");
        placed.Status.ShouldBe(HttpStatusCode.Created, placed.Body.ToString());
        var order = placed.Body.Data();
        order.GetProperty("status").GetString().ShouldBe("Paid");   // not waiting for a payment: never called off after 15 minutes
        order.GetProperty("paymentMethod").GetString().ShouldBe("CashOnDelivery");
        var code = order.GetProperty("code").GetString()!;
        Wallet((await partner.GetAsync("/api/v1/wallet")).Body.Data(), "PartnerStore").GetProperty("pending").GetDecimal().ShouldBe(300 - 15);
        (await customer.GetAsync("/api/v1/receipts")).Body.Data().GetArrayLength().ShouldBe(0);   // nothing paid yet

        await kit.DeliverAsync(partner, code);
        var receipts = (await customer.GetAsync("/api/v1/receipts")).Body.Data();
        receipts.GetArrayLength().ShouldBe(1);
        receipts[0].GetProperty("amount").GetDecimal().ShouldBe(order.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task A_cancelled_cash_on_delivery_order_refunds_nothing_and_takes_the_earning_back()
    {
        var (partner, customer) = await CartAsync();
        var code = (await ShopKit.CheckoutAsync(customer, payment: "cod")).Body.Data().GetProperty("code").GetString()!;
        await api.CancelThroughRequestAsync(customer, "order", code);

        (await customer.GetAsync("/api/v1/wallet")).Body.Data().EnumerateArray().Where(w => w.GetProperty("type").GetString() == "Customer")
            .Sum(w => w.GetProperty("available").GetDecimal()).ShouldBe(0);   // no refund: nothing was paid
        Wallet((await partner.GetAsync("/api/v1/wallet")).Body.Data(), "PartnerStore").GetProperty("pending").GetDecimal().ShouldBe(0);
        (await customer.GetAsync("/api/v1/receipts")).Body.Data().GetArrayLength().ShouldBe(0);
    }
}
