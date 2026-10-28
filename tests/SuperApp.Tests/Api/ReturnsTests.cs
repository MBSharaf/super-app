using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>Returns and exchanges (owner's decisions, 2026-10-27): an item with a problem, within 24 hours of delivery, the store pays; store / Super App decisions.</summary>
[Trait("Category", "Database")]
public sealed class ReturnsTests(DemoApiFactory api) : IClassFixture<DemoApiFactory>
{
    private readonly ShopKit kit = new(api);
    private readonly PartnerKit partners = new(api);

    /// <summary>A partner product in M and L; a customer bought one M (plus optional extra lines) and it was delivered.</summary>
    private async Task<(ApiClient Partner, ApiClient Customer, string Order, JsonElement Product)> DeliveredAsync(params long[] extraVariants)
    {
        var (partner, _) = await partners.PersonAsync(partner: true);
        await partner.GetAsync("/api/v1/partner/store");
        var variants = new[] { "M", "L" }.Select(size => new { id = (long?)null, size, colorName = "Black", colorHex = (string?)null, sku = (string?)null, stock = 5, priceOverride = (decimal?)null }).ToArray();
        var product = (await partner.PostAsync("/api/v1/partner/store/products", new { name = "Training Top", categoryKey = "kits", price = 400m, lowStockAt = 1, status = "Published", specs = Array.Empty<object>(), variants })).Body.Data();
        var id = product.GetProperty("id").GetInt64();
        product = (await partner.SendFormAsync(HttpMethod.Post, $"/api/v1/partner/store/products/{id}/images", new { }, ("photos", "t.png", ShopKit.Png()))).Body.Data();
        await kit.ApproveAsync(id);
        var (customer, _) = await kit.PersonAsync();
        await customer.PostAsync("/api/v1/cart/items", new { productVariantId = product.GetProperty("variants")[0].GetProperty("id").GetInt64(), qty = 1 });
        foreach (var v in extraVariants) { await customer.PostAsync("/api/v1/cart/items", new { productVariantId = v, qty = 1 }); }
        var order = (await ShopKit.CheckoutAsync(customer)).Body.Data().GetProperty("code").GetString()!;
        await kit.DeliverAsync(partner, order);
        return (partner, customer, order, product);
    }

    private static JsonElement Wallet(JsonElement wallets, string type) => wallets.EnumerateArray().Single(w => w.GetProperty("type").GetString() == type);

    private async Task<int> StockAsync(JsonElement product, int variant) =>
        await api.DbAsync(db => db.Set<ProductVariant>().Where(v => v.Id == product.GetProperty("variants")[variant].GetProperty("id").GetInt64()).Select(v => v.Stock).SingleAsync());

    [Fact]
    public async Task A_store_refund_is_approved_picked_up_and_received()
    {
        var (partner, customer, order, product) = await DeliveredAsync();
        var returnable = (await customer.GetAsync($"/api/v1/orders/{order}/returnable")).Body.Data();
        returnable.GetArrayLength().ShouldBe(1);
        var item = returnable[0].GetProperty("orderItemId").GetInt64();
        (await customer.PostAsync("/api/v1/returns", new { orderCode = order, reason = "ChangedMyMind", items = new[] { new { orderItemId = item, qty = 1, action = "Refund" } } }))
            .Body.Code().ShouldBe("problem_only");
        (await customer.PostAsync("/api/v1/returns", new { orderCode = order, reason = "WrongSize", items = new[] { new { orderItemId = item, qty = 1, action = "Refund" } } }))
            .Body.Code().ShouldBe("problem_only");
        var created = await customer.PostAsync("/api/v1/returns", new { orderCode = order, reason = "FaultyOrDamaged", detail = "Torn seam", items = new[] { new { orderItemId = item, qty = 1, action = "Refund" } } });
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        var ret = created.Body.Data();
        ret.GetProperty("customerPaysDelivery").GetBoolean().ShouldBeFalse();
        ret.GetProperty("refundAmount").GetDecimal().ShouldBe(400);
        var code = ret.GetProperty("code").GetString()!;
        (await customer.PostAsync("/api/v1/returns", new { orderCode = order, reason = "NotAsDescribed", items = new[] { new { orderItemId = item, qty = 1, action = "Refund" } } })).Body.Code().ShouldBe("qty_left");

        (await partner.GetAsync("/api/v1/partner/returns")).Body.Data().GetArrayLength().ShouldBe(1);
        (await partner.PostAsync($"/api/v1/partner/returns/{code}/received")).Body.Code().ShouldBe("wrong_state");
        (await partner.PostAsync($"/api/v1/partner/returns/{code}/approve")).Body.Data().GetProperty("pickupTracking").GetString()!.ShouldStartWith("SIM");
        var stock = await StockAsync(product, 0);
        var received = await partner.PostAsync($"/api/v1/partner/returns/{code}/received");
        received.Status.ShouldBe(HttpStatusCode.OK, received.Body.ToString());
        received.Body.Data().GetProperty("status").GetString().ShouldBe("Refunded");
        (await StockAsync(product, 0)).ShouldBe(stock + 1);
        (await customer.GetAsync($"/api/v1/returns/{code}")).Body.Data().GetProperty("status").GetString().ShouldBe("Refunded");
        // The customer gets the 400 back; the store loses its held 380 (5% commission) and owes the other 20 and the 60 pickup
        Wallet((await customer.GetAsync("/api/v1/wallet")).Body.Data(), "Customer").GetProperty("available").GetDecimal().ShouldBe(400);
        var store = Wallet((await partner.GetAsync("/api/v1/wallet")).Body.Data(), "PartnerStore");
        store.GetProperty("pending").GetDecimal().ShouldBe(-80);   // a debt taken from its next released earnings
        store.GetProperty("available").GetDecimal().ShouldBe(0);
        (await customer.GetAsync("/api/v1/returns/mine")).Body.Data().GetArrayLength().ShouldBe(1);
        var (other, _) = await kit.PersonAsync();
        (await other.GetAsync($"/api/v1/returns/{code}")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Exchanges_swap_the_size_and_rejections_need_a_reason()
    {
        var (partner, customer, order, product) = await DeliveredAsync();
        var item = (await customer.GetAsync($"/api/v1/orders/{order}/returnable")).Body.Data()[0].GetProperty("orderItemId").GetInt64();
        (await customer.PostAsync("/api/v1/returns", new { orderCode = order, reason = "WrongItemOrPrint", items = new[] { new { orderItemId = item, qty = 1, action = "Exchange" } } })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var code = (await customer.PostAsync("/api/v1/returns", new { orderCode = order, reason = "WrongItemOrPrint", items = new[] { new { orderItemId = item, qty = 1, action = "Exchange", newSize = "L" } } }))
            .Body.Data().GetProperty("code").GetString()!;
        await partner.PostAsync($"/api/v1/partner/returns/{code}/approve");
        var (m, l) = (await StockAsync(product, 0), await StockAsync(product, 1));
        (await partner.PostAsync($"/api/v1/partner/returns/{code}/received")).Body.Data().GetProperty("status").GetString().ShouldBe("Exchanged");
        (await StockAsync(product, 0)).ShouldBe(m + 1);
        (await StockAsync(product, 1)).ShouldBe(l - 1);
        var store = Wallet((await partner.GetAsync("/api/v1/wallet")).Body.Data(), "PartnerStore");
        store.GetProperty("pending").GetDecimal().ShouldBe(380 - 120);   // the sale stands; it owes the pickup and the new parcel
        var job = api.Services.GetRequiredService<SuperApp.Infrastructure.Payments.MoneyReleaseJob>();
        await job.RunOnceAsync(CancellationToken.None);   // the earning is released and the debt paid from it
        store = Wallet((await partner.GetAsync("/api/v1/wallet")).Body.Data(), "PartnerStore");
        store.GetProperty("available").GetDecimal().ShouldBe(380 - 120);
        store.GetProperty("pending").GetDecimal().ShouldBe(0);

        var (partner2, customer2, order2, _) = await DeliveredAsync();
        var item2 = (await customer2.GetAsync($"/api/v1/orders/{order2}/returnable")).Body.Data()[0].GetProperty("orderItemId").GetInt64();
        var code2 = (await customer2.PostAsync("/api/v1/returns", new { orderCode = order2, reason = "NotAsDescribed", items = new[] { new { orderItemId = item2, qty = 1, action = "Refund" } } }))
            .Body.Data().GetProperty("code").GetString()!;
        (await partner2.PostAsync($"/api/v1/partner/returns/{code2}/reject", new { reason = "no" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await partner2.PostAsync($"/api/v1/partner/returns/{code2}/reject", new { reason = "The photos show it was worn." })).Body.Data().GetProperty("status").GetString().ShouldBe("Rejected");
        (await api.DbAsync(db => db.Set<SuperApp.Domain.Entities.Money.LedgerEntry>().Where(e => e.RefType == "OrderItem" && e.RefId == item2).Select(e => e.AvailableAt).FirstAsync())).ShouldNotBeNull();   // held again until the window ends
        (await partner.PostAsync($"/api/v1/partner/returns/{code2}/approve")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Printed_items_come_back_only_for_defects_and_super_app_sends_them_for_reprint()
    {
        var (customer, _) = await kit.PersonAsync();
        await customer.PostAsync("/api/v1/cart/items", new { designId = await kit.DesignIdAsync("No Days Off"), size = "M", color = "Black", qty = 1 });
        var order = (await ShopKit.CheckoutAsync(customer)).Body.Data().GetProperty("code").GetString()!;
        await api.DbAsync(db => db.Set<OrderItem>().Where(i => i.Order.Code == order).ExecuteUpdateAsync(u => u.SetProperty(i => i.Status, OrderItemStatus.Delivered).SetProperty(i => i.DeliveredAt, DateTime.UtcNow)));
        var item = (await customer.GetAsync($"/api/v1/orders/{order}/returnable")).Body.Data()[0];
        item.GetProperty("actions").EnumerateArray().Select(a => a.GetString()).ShouldBe(["Reprint", "Refund"]);
        var id = item.GetProperty("orderItemId").GetInt64();
        (await customer.PostAsync("/api/v1/returns", new { orderCode = order, reason = "ChangedMyMind", items = new[] { new { orderItemId = id, qty = 1, action = "Refund" } } })).Body.Code().ShouldBe("problem_only");
        (await customer.PostAsync("/api/v1/returns", new { orderCode = order, reason = "FaultyOrDamaged", items = new[] { new { orderItemId = id, qty = 1, action = "Exchange", newSize = "L" } } })).Body.Code().ShouldBe("action_not_allowed");
        var code = (await customer.PostAsync("/api/v1/returns", new { orderCode = order, reason = "WrongItemOrPrint", detail = "Print is blurry", items = new[] { new { orderItemId = id, qty = 1, action = "Reprint" } } }))
            .Body.Data().GetProperty("code").GetString()!;
        var ceo = await api.CeoAsync();
        (await ceo.GetAsync("/api/v1/admin/returns?status=Requested")).Body.Data().EnumerateArray().ShouldContain(r => r.GetProperty("code").GetString() == code);
        await ceo.PostAsync($"/api/v1/admin/returns/{code}/approve");
        (await ceo.PostAsync($"/api/v1/admin/returns/{code}/received")).Body.Data().GetProperty("status").GetString().ShouldBe("Reprinting");
        var job = await api.DbAsync(db => db.Set<Job>().SingleAsync(j => j.OrderItem.Order.Code == order));
        job.Stage.ShouldBe(JobStage.New);
        job.ReprintCount.ShouldBe((byte)1);
    }

    [Fact]
    public async Task One_return_is_for_one_store_and_old_deliveries_cannot_be_returned()
    {
        var bottle = (await kit.VariantsAsync("sh-bottle"))[0].Id;
        var (_, customer, order, _) = await DeliveredAsync(bottle);
        await api.DbAsync(db => db.Set<OrderItem>().Where(i => i.Order.Code == order && i.ProductVariantId == bottle)
            .ExecuteUpdateAsync(u => u.SetProperty(i => i.Status, OrderItemStatus.Delivered).SetProperty(i => i.DeliveredAt, DateTime.UtcNow)));
        var items = (await customer.GetAsync($"/api/v1/orders/{order}/returnable")).Body.Data().EnumerateArray().Select(i => i.GetProperty("orderItemId").GetInt64()).ToArray();
        items.Length.ShouldBe(2);
        (await customer.PostAsync("/api/v1/returns", new { orderCode = order, reason = "FaultyOrDamaged", items = items.Select(i => new { orderItemId = i, qty = 1, action = "Refund" }).ToArray() }))
            .Body.Code().ShouldBe("one_store_per_return");
        await api.DbAsync(db => db.Set<OrderItem>().Where(i => i.Order.Code == order).ExecuteUpdateAsync(u => u.SetProperty(i => i.DeliveredAt, DateTime.UtcNow.AddHours(-25))));
        (await customer.PostAsync("/api/v1/returns", new { orderCode = order, reason = "FaultyOrDamaged", items = new[] { new { orderItemId = items[0], qty = 1, action = "Refund" } } }))
            .Body.Code().ShouldBe("outside_window");
        var (other, _) = await kit.PersonAsync();
        (await other.GetAsync($"/api/v1/orders/{order}/returnable")).Status.ShouldBe(HttpStatusCode.NotFound);
    }
}
