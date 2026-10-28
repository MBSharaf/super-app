using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>Cart, codes, wishlist, checkout and "My orders" on the demo data (API §4.2).</summary>
[Trait("Category", "Database")]
public sealed class CartOrdersTests(DemoApiFactory api) : IClassFixture<DemoApiFactory>
{
    private readonly ShopKit kit = new(api);

    private async Task<long> VariantAsync(string slug) => (await kit.VariantsAsync(slug))[0].Id;

    [Fact]
    public async Task The_cart_keeps_live_prices_stock_and_delivery_fees()
    {
        var (person, _) = await kit.PersonAsync();
        var ball = await VariantAsync("sh-match-ball");
        var cart = (await person.PostAsync("/api/v1/cart/items", new { productVariantId = ball, qty = 1 })).Body.Data();
        cart.GetProperty("subtotal").GetDecimal().ShouldBe(749);
        cart.GetProperty("deliveryFee").GetDecimal().ShouldBe(60);
        cart.GetProperty("expressFee").GetDecimal().ShouldBe(120);
        cart = (await person.PostAsync("/api/v1/cart/items", new { productVariantId = ball, qty = 1 })).Body.Data();
        cart.GetProperty("lines").GetArrayLength().ShouldBe(1);
        cart.GetProperty("freeDeliveryGap").GetDecimal().ShouldBe(2);
        var line = cart.GetProperty("lines")[0].GetProperty("id").GetInt64();
        cart = (await person.SendAsync(HttpMethod.Patch, $"/api/v1/cart/items/{line}", new { qty = 3 })).Body.Data();
        cart.GetProperty("deliveryFee").GetDecimal().ShouldBe(0);
        cart.GetProperty("expressFee").GetDecimal().ShouldBe(60);
        (await person.SendAsync(HttpMethod.Patch, $"/api/v1/cart/items/{line}", new { qty = 21 })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var (other, _) = await kit.PersonAsync();
        (await other.SendAsync(HttpMethod.Patch, $"/api/v1/cart/items/{line}", new { qty = 1 })).Status.ShouldBe(HttpStatusCode.NotFound);

        var socks = await VariantAsync("tp-socks");
        await api.DbAsync(db => db.Set<ProductVariant>().Where(v => v.Id == socks).ExecuteUpdateAsync(u => u.SetProperty(v => v.Stock, 2)));
        (await person.PostAsync("/api/v1/cart/items", new { productVariantId = socks, qty = 3 })).Body.Code().ShouldBe("out_of_stock");

        var design = await kit.DesignIdAsync("Keep Calm & Press High");
        (await person.PostAsync("/api/v1/cart/items", new { designId = design, qty = 1 })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await person.PostAsync("/api/v1/cart/items", new { designId = design, size = "M", color = "Pink", qty = 1 })).Body.Code().ShouldBe("bad_color");
        cart = (await person.PostAsync("/api/v1/cart/items", new { designId = design, size = "2XL", color = "black", qty = 1 })).Body.Data();
        var printed = cart.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("kind").GetString() == "design");
        printed.GetProperty("unitPrice").GetDecimal().ShouldBe(450);   // 420 + big-size extra 30
        printed.GetProperty("color").GetString().ShouldBe("Black");
        cart = (await person.SendAsync(HttpMethod.Delete, $"/api/v1/cart/items/{printed.GetProperty("id").GetInt64()}")).Body.Data();
        cart.GetProperty("lines").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Codes_are_checked_against_the_cart_and_counted_once_used()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var kooraStore = await api.DbAsync(db => db.Set<Store>().Where(s => s.Handle == "koora-ink").Select(s => new { s.Id, s.OwnerUserId }).SingleAsync());
        await api.DbAsync(db =>
        {
            db.Set<PromoCode>().AddRange(
                new PromoCode { Code = "SUPER10", Funding = CodeFunding.SuperAppFunded, Scope = CodeScope.Shop, DiscountType = DiscountType.Percent, Value = 10, MinSpend = 500, StartsOn = today.AddDays(-1) },
                new PromoCode { Code = "OLDCODE", Funding = CodeFunding.SuperAppFunded, Scope = CodeScope.Both, DiscountType = DiscountType.Fixed, Value = 50, StartsOn = today.AddDays(-30), EndsOn = today.AddDays(-2) },
                new PromoCode { Code = "KOORA20", Funding = CodeFunding.OwnerFunded, OwnerUserId = kooraStore.OwnerUserId, StoreId = kooraStore.Id, Scope = CodeScope.Shop, DiscountType = DiscountType.Fixed, Value = 20, StartsOn = today.AddDays(-1) },
                new PromoCode { Code = "FREESHIP", Funding = CodeFunding.SuperAppFunded, Scope = CodeScope.Shop, DiscountType = DiscountType.FreeDelivery, Value = 0, StartsOn = today.AddDays(-1) });
            return db.SaveChangesAsync();
        });
        var (person, _) = await kit.PersonAsync();
        await person.PostAsync("/api/v1/cart/items", new { productVariantId = await VariantAsync("sh-shin"), qty = 1 });
        (await person.PostAsync("/api/v1/cart/promo", new { code = "super10" })).Body.Code().ShouldBe("min_spend");
        await person.PostAsync("/api/v1/cart/items", new { productVariantId = await VariantAsync("sh-bottle"), qty = 1 });
        (await person.PostAsync("/api/v1/cart/promo", new { code = "SUPER10" })).Body.Data().GetProperty("discount").GetDecimal().ShouldBe(77);
        (await person.GetAsync("/api/v1/cart?promo=SUPER10")).Body.Data().GetProperty("promo").GetProperty("funding").GetString().ShouldBe("SuperApp");
        (await person.GetAsync("/api/v1/cart?promo=OLDCODE")).Body.Data().GetProperty("promoError").GetString().ShouldNotBeNull();
        (await person.PostAsync("/api/v1/cart/promo", new { code = "OLDCODE" })).Body.Code().ShouldBe("code_expired");
        (await person.PostAsync("/api/v1/cart/promo", new { code = "NOPE1" })).Body.Code().ShouldBe("invalid_code");
        (await person.PostAsync("/api/v1/cart/promo", new { code = "KOORA20" })).Body.Code().ShouldBe("code_not_for_cart");
        (await person.PostAsync("/api/v1/cart/promo", new { code = "FREESHIP" })).Body.Data().GetProperty("deliveryFee").GetDecimal().ShouldBe(0);

        var placed = await ShopKit.CheckoutAsync(person, promo: "SUPER10");
        placed.Status.ShouldBe(HttpStatusCode.Created, placed.Body.ToString());
        placed.Body.Data().GetProperty("order").GetProperty("discount").GetDecimal().ShouldBe(77);
        (await api.DbAsync(db => db.Set<PromoCode>().Where(p => p.Code == "SUPER10").Select(p => p.UsesCount).SingleAsync())).ShouldBe(1);
        (await person.PostAsync($"/api/v1/orders/{placed.Body.Data().GetProperty("code").GetString()}/cancel")).Status.ShouldBe(HttpStatusCode.OK);
        (await api.DbAsync(db => db.Set<PromoCode>().Where(p => p.Code == "SUPER10").Select(p => p.UsesCount).SingleAsync())).ShouldBe(0);

        await person.PostAsync("/api/v1/cart/items", new { designId = await kit.DesignIdAsync("Keep Calm & Press High"), size = "M", color = "White", qty = 1 });
        var store = (await person.PostAsync("/api/v1/cart/promo", new { code = "KOORA20" })).Body.Data().GetProperty("promo");
        store.GetProperty("funding").GetString().ShouldBe("Store");
        store.GetProperty("discount").GetDecimal().ShouldBe(20);
    }

    [Fact]
    public async Task Checkout_places_the_order_takes_stock_routes_printing_and_is_idempotent()
    {
        var (person, _) = await kit.PersonAsync();
        var ball = (await kit.VariantsAsync("sh-match-ball"))[0];
        await person.PostAsync("/api/v1/cart/items", new { productVariantId = ball.Id, qty = 2 });
        await person.PostAsync("/api/v1/cart/items", new { productVariantId = await VariantAsync("tp-bibs"), qty = 1 });
        await person.PostAsync("/api/v1/cart/items", new { designId = await kit.DesignIdAsync("Keep Calm & Press High"), size = "M", color = "Black", qty = 2 });
        (await ShopKit.CheckoutAsync(person, key: "")).Body.Code().ShouldBe("idempotency_key");

        var key = Guid.NewGuid().ToString("N");
        var placed = await ShopKit.CheckoutAsync(person, key: key);
        placed.Status.ShouldBe(HttpStatusCode.Created, placed.Body.ToString());
        var result = placed.Body.Data();
        var code = result.GetProperty("code").GetString()!;
        code.ShouldStartWith("ORD-");
        result.GetProperty("status").GetString().ShouldBe("Paid");
        result.GetProperty("paymentSimulated").GetBoolean().ShouldBeTrue();
        result.GetProperty("total").GetDecimal().ShouldBe((2 * 749) + 450 + (2 * 420));   // over 1,500: free delivery
        result.GetProperty("order").GetProperty("groups").GetArrayLength().ShouldBe(3);
        (await ShopKit.CheckoutAsync(person, key: key)).Body.Data().GetProperty("code").GetString().ShouldBe(code);
        (await ShopKit.CheckoutAsync(person)).Body.Code().ShouldBe("empty_cart");
        (await person.GetAsync("/api/v1/cart")).Body.Data().GetProperty("lines").GetArrayLength().ShouldBe(0);

        (await kit.VariantsAsync("sh-match-ball"))[0].Stock.ShouldBe(ball.Stock - 2);
        var items = await api.DbAsync(db => db.Set<OrderItem>().Where(i => i.Order.Code == code).ToListAsync());
        items.Single(i => i.ProductVariantId == ball.Id).CommissionAmount.ShouldBe(0);                     // official store
        items.Single(i => i.UnitPrice == 450).CommissionAmount.ShouldBe(22.5m); // partner store, 5%
        var printed = items.Single(i => i.Kind == OrderItemKind.PrintedDesign);
        printed.BaseAmount.ShouldBe(265);                 // tee 300 − Elite discount 35
        printed.SellerEarning.ShouldBe(2 * (420 - 265));
        printed.CommissionAmount.ShouldBe(2 * (265 - 210));
        var job = await api.DbAsync(db => db.Set<Job>().Include(j => j.Factory).SingleAsync(j => j.OrderItemId == printed.Id));
        job.Stage.ShouldBe(JobStage.New);
        job.Factory.Name.ShouldBe("Nile DTF Works");
        (await api.DbAsync(db => db.Set<Fulfilment>().CountAsync(f => f.Order.Code == code))).ShouldBe(2);
        var told = await api.DbAsync(db => db.Set<Notification>().Where(n => n.Title.Contains(code) || n.Body!.Contains(code)).CountAsync());
        told.ShouldBeGreaterThanOrEqualTo(3);   // customer, the partner store, the seller (and the factory)
    }

    [Fact]
    public async Task Cash_on_delivery_is_refused_and_switches_and_blocked_accounts_stop_checkout()
    {
        var (person, userId) = await kit.PersonAsync();
        await person.PostAsync("/api/v1/cart/items", new { designId = await kit.DesignIdAsync("Giza Run Club"), size = "L", color = "White", qty = 1 });
        (await ShopKit.CheckoutAsync(person, payment: "cod")).Status.ShouldBe(HttpStatusCode.BadRequest);   // online payment only
        (await person.GetAsync("/api/v1/cart")).Body.Data().GetProperty("lines").GetArrayLength().ShouldBe(1);
        await person.SendAsync(HttpMethod.Delete, $"/api/v1/cart/items/{(await person.GetAsync("/api/v1/cart")).Body.Data().GetProperty("lines")[0].GetProperty("id").GetInt64()}");

        await person.PostAsync("/api/v1/cart/items", new { productVariantId = await VariantAsync("sh-cones"), qty = 1 });
        await SwitchAsync("kill.shop", false);
        try
        {
            (await ShopKit.CheckoutAsync(person)).Status.ShouldBe((HttpStatusCode)423);
        }
        finally
        {
            await SwitchAsync("kill.shop", true);
        }
        var wallet = await ShopKit.CheckoutAsync(person, payment: "wallet");
        wallet.Body.Code().ShouldBe("insufficient_funds");   // an empty wallet: the order is called off and the stock comes back
        (await person.GetAsync("/api/v1/cart")).Body.Data().GetProperty("lines").GetArrayLength().ShouldBe(0);

        await person.PostAsync("/api/v1/cart/items", new { productVariantId = await VariantAsync("sh-cones"), qty = 1 });
        await api.DbAsync(db => db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, UserStatus.BlockedFromBookingAndOrdering)));
        (await ShopKit.CheckoutAsync(person)).Body.Code().ShouldBe("account_blocked");
    }

    [Fact]
    public async Task My_orders_show_parcels_receipts_and_cancel_only_before_work_starts()
    {
        var (person, _) = await kit.PersonAsync();
        var cones = (await kit.VariantsAsync("sh-cones"))[0];
        await person.PostAsync("/api/v1/cart/items", new { productVariantId = cones.Id, qty = 1 });
        var code = (await ShopKit.CheckoutAsync(person)).Body.Data().GetProperty("code").GetString()!;
        (await person.GetAsync("/api/v1/orders/mine")).Body.Data()[0].GetProperty("code").GetString().ShouldBe(code);
        (await person.GetAsync($"/api/v1/orders/{code}")).Body.Data().GetProperty("canCancel").GetBoolean().ShouldBeTrue();
        var pdf = await person.Http.GetAsync($"/api/v1/orders/{code}/receipt.pdf");
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await pdf.Content.ReadAsByteArrayAsync())[..4].ShouldBe("%PDF"u8.ToArray());
        var (other, _) = await kit.PersonAsync();
        (await other.GetAsync($"/api/v1/orders/{code}")).Status.ShouldBe(HttpStatusCode.NotFound);

        var cancelled = (await person.PostAsync($"/api/v1/orders/{code}/cancel")).Body.Data();
        cancelled.GetProperty("status").GetString().ShouldBe("Cancelled");
        (await kit.VariantsAsync("sh-cones")).Single(v => v.Id == cones.Id).Stock.ShouldBe(cones.Stock);
        (await person.PostAsync($"/api/v1/orders/{code}/cancel")).Body.Code().ShouldBe("order_closed");

        await person.PostAsync("/api/v1/cart/items", new { designId = await kit.DesignIdAsync("No Days Off"), size = "S", color = "Black", qty = 1 });
        var printed = (await ShopKit.CheckoutAsync(person)).Body.Data().GetProperty("code").GetString()!;
        await api.DbAsync(db => db.Set<Job>().Where(j => j.OrderItem.Order.Code == printed).ExecuteUpdateAsync(u => u.SetProperty(j => j.Stage, JobStage.Printing)));
        (await person.PostAsync($"/api/v1/orders/{printed}/cancel")).Body.Code().ShouldBe("too_late");
        (await person.GetAsync("/api/v1/orders/mine?status=cancelled")).Body.Data().GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task The_wishlist_saves_products_and_designs_once()
    {
        var (person, _) = await kit.PersonAsync();
        (await person.PostAsync("/api/v1/wishlist", new { productSlug = "sh-copa" })).Status.ShouldBe(HttpStatusCode.Created);
        (await person.PostAsync("/api/v1/wishlist", new { productSlug = "sh-copa" })).Status.ShouldBe(HttpStatusCode.Created);
        var list = (await person.PostAsync("/api/v1/wishlist", new { designId = await kit.DesignIdAsync("Ya Habibi") })).Body.Data();
        list.GetArrayLength().ShouldBe(2);
        list.EnumerateArray().ShouldAllBe(w => w.GetProperty("card").ValueKind == System.Text.Json.JsonValueKind.Object);
        (await person.PostAsync("/api/v1/wishlist", new { productSlug = "nothing-here" })).Status.ShouldBe(HttpStatusCode.NotFound);
        (await person.PostAsync("/api/v1/wishlist", new { })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var id = list[0].GetProperty("id").GetInt64();
        (await person.SendAsync(HttpMethod.Delete, $"/api/v1/wishlist/{id}")).Body.Data().GetArrayLength().ShouldBe(1);
        (await person.SendAsync(HttpMethod.Delete, $"/api/v1/wishlist/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    private Task<int> SwitchAsync(string key, bool on) => api.DbAsync(async db =>
    {
        var flag = await db.Set<FeatureFlag>().SingleAsync(f => f.Key == key);
        flag.IsEnabled = on;
        return await db.SaveChangesAsync();
    });
}
