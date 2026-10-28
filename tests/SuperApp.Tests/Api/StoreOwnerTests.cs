using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>Partner shops, sellers' own products, the official store and admin hide (API §4.4, §4.5, §7.2).</summary>
[Trait("Category", "Database")]
public sealed class StoreOwnerTests(DemoApiFactory api) : IClassFixture<DemoApiFactory>
{
    private readonly ShopKit kit = new(api);
    private readonly PartnerKit partners = new(api);

    private static object Product(string name = "Grip Socks", string status = "Published", object[]? variants = null) => new
    {
        name, categoryKey = "accessories", brand = "Turf", description = "Anti-slip socks.", price = 250m, compareAtPrice = 300m, lowStockAt = 3, status,
        specs = new[] { new { label = "Material", value = "Cotton" } },
        variants = variants ?? [new { id = (long?)null, size = "M", colorName = "Black", colorHex = "#000000", sku = (string?)null, stock = 10, priceOverride = (decimal?)null },
            new { id = (long?)null, size = "L", colorName = "Black", colorHex = "#000000", sku = (string?)null, stock = 5, priceOverride = (decimal?)null }],
    };

    private async Task<(ApiClient Partner, string Handle, JsonElement Product)> PartnerProductAsync()
    {
        var (partner, _) = await partners.PersonAsync(partner: true);
        var store = (await partner.GetAsync("/api/v1/partner/store")).Body.Data();
        store.GetProperty("type").GetString().ShouldBe("PitchPartner");
        var added = await partner.PostAsync("/api/v1/partner/store/products", Product());
        added.Status.ShouldBe(HttpStatusCode.Created, added.Body.ToString());
        var id = added.Body.Data().GetProperty("id").GetInt64();
        var photo = await partner.SendFormAsync(HttpMethod.Post, $"/api/v1/partner/store/products/{id}/images", new { }, ("photos", "socks.png", ShopKit.Png()));
        photo.Status.ShouldBe(HttpStatusCode.Created, photo.Body.ToString());
        photo.Body.Data().GetProperty("status").GetString().ShouldBe("WaitingForReview");
        await kit.ApproveAsync(id);
        return (partner, store.GetProperty("handle").GetString()!, (await partner.GetAsync($"/api/v1/partner/store/products/{id}")).Body.Data());
    }

    [Fact]
    public async Task A_partner_runs_a_shop_with_products_photos_and_stock()
    {
        var (partner, handle, product) = await PartnerProductAsync();
        var id = product.GetProperty("id").GetInt64();
        product.GetProperty("variants").EnumerateArray().ShouldAllBe(v => !string.IsNullOrEmpty(v.GetProperty("sku").GetString()));
        product.GetProperty("stock").GetInt32().ShouldBe(15);
        (await partner.SendAsync(HttpMethod.Put, "/api/v1/partner/store", new { name = "My Arena Shop", handle = "koora-ink", coverPositionY = 50 })).Body.Code().ShouldBe("handle_taken");
        (await partner.SendAsync(HttpMethod.Put, "/api/v1/partner/store", new { name = "My Arena Shop", handle, tagline = "Matchday gear", coverPositionY = 40 })).Status.ShouldBe(HttpStatusCode.OK);

        var image = product.GetProperty("images")[0].GetProperty("id").GetInt64();
        (await partner.SendAsync(HttpMethod.Delete, $"/api/v1/partner/store/products/{id}/images/{image}")).Body.Code().ShouldBe("last_photo");
        var variant = product.GetProperty("variants")[0].GetProperty("id").GetInt64();
        (await partner.PostAsync($"/api/v1/partner/store/products/{id}/restock", new { variantId = variant, add = 5 })).Body.Data().GetProperty("stock").GetInt32().ShouldBe(20);
        var keepOne = new object[] { new { id = (long?)variant, size = "M", colorName = "Black", colorHex = "#000000", sku = (string?)null, stock = 15, priceOverride = (decimal?)null } };
        (await partner.SendAsync(HttpMethod.Put, $"/api/v1/partner/store/products/{id}", Product(variants: keepOne))).Body.Data().GetProperty("variants").GetArrayLength().ShouldBe(1);
        (await partner.PostAsync("/api/v1/partner/store/products", Product(variants: []))).Status.ShouldBe(HttpStatusCode.BadRequest);

        var guest = await api.ClientAsync();
        (await guest.GetAsync($"/api/v1/shop/products?store={handle}")).Body.Data().GetArrayLength().ShouldBe(1);
        var (stranger, _) = await kit.PersonAsync();
        (await stranger.GetAsync("/api/v1/partner/store/products")).Body.Code().ShouldBe("not_partner");
        (await partner.SendAsync(HttpMethod.Delete, $"/api/v1/partner/store/products/{id}")).Status.ShouldBe(HttpStatusCode.OK);
        (await guest.GetAsync($"/api/v1/shop/products?store={handle}")).Body.Data().GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task A_store_packs_ships_with_the_courier_and_delivers_its_part()
    {
        var (partner, _, product) = await PartnerProductAsync();
        var (customer, _) = await kit.PersonAsync();
        await customer.PostAsync("/api/v1/cart/items", new { productVariantId = product.GetProperty("variants")[0].GetProperty("id").GetInt64(), qty = 2 });
        await customer.PostAsync("/api/v1/cart/items", new { productVariantId = (await kit.VariantsAsync("sh-bottle"))[0].Id, qty = 1 });
        var code = (await ShopKit.CheckoutAsync(customer)).Body.Data().GetProperty("code").GetString()!;

        var list = (await partner.GetAsync("/api/v1/partner/store/orders?status=open")).Body.Data();
        list.GetArrayLength().ShouldBe(1);
        list[0].GetProperty("items").GetArrayLength().ShouldBe(1);
        list[0].GetProperty("earnings").GetDecimal().ShouldBe(500 - 25);   // 5% commission
        (await partner.PostAsync($"/api/v1/partner/store/orders/{code}/ship")).Body.Code().ShouldBe("wrong_state");
        (await partner.PostAsync($"/api/v1/partner/store/orders/{code}/pack")).Body.Data().GetProperty("status").GetString().ShouldBe("Packed");
        var shipped = (await partner.PostAsync($"/api/v1/partner/store/orders/{code}/ship")).Body.Data();
        shipped.GetProperty("trackingNumber").GetString()!.ShouldStartWith("SIM");
        var cod = await api.DbAsync(db => db.Set<SuperApp.Domain.Entities.Shipping.Shipment>().Where(s => s.Order.Code == code).Select(s => s.CodAmount).SingleAsync());
        cod.ShouldBe(0);   // paid online: the courier collects nothing
        var order = (await customer.GetAsync($"/api/v1/orders/{code}")).Body.Data();
        order.GetProperty("canCancel").GetBoolean().ShouldBeFalse();
        order.GetProperty("groups").EnumerateArray().ShouldContain(g => g.GetProperty("trackingNumber").ValueKind == JsonValueKind.String);
        (await partner.PostAsync($"/api/v1/partner/store/orders/{code}/deliver")).Body.Code().ShouldBe("courier_delivers");
        var parcel = await api.DbAsync(db => db.Set<SuperApp.Domain.Entities.Shipping.Shipment>().Where(s => s.Order.Code == code).Select(s => s.Id).SingleAsync());
        var ceo = await api.CeoAsync();
        foreach (var status in new[] { "PickedUp", "OutForDelivery", "Delivered" })
        {
            await ceo.PostAsync($"/api/v1/admin/shipments/{parcel}/simulate", new { status });
        }
        (await customer.GetAsync($"/api/v1/orders/{code}")).Body.Data().GetProperty("status").GetString().ShouldBe("InProgress");
    }

    [Fact]
    public async Task A_store_cancels_its_part_with_a_reason_and_the_stock_comes_back()
    {
        var (partner, _, product) = await PartnerProductAsync();
        var variant = product.GetProperty("variants")[0].GetProperty("id").GetInt64();
        var (customer, customerId) = await kit.PersonAsync();
        await customer.PostAsync("/api/v1/cart/items", new { productVariantId = variant, qty = 3 });
        var code = (await ShopKit.CheckoutAsync(customer)).Body.Data().GetProperty("code").GetString()!;
        (await partner.PostAsync($"/api/v1/partner/store/orders/{code}/cancel", new { reason = "" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await partner.PostAsync($"/api/v1/partner/store/orders/{code}/cancel", new { reason = "Out of this size" })).Body.Data().GetProperty("status").GetString().ShouldBe("Cancelled");
        (await partner.GetAsync($"/api/v1/partner/store/products/{product.GetProperty("id").GetInt64()}")).Body.Data().GetProperty("stock").GetInt32().ShouldBe(15);
        (await customer.GetAsync($"/api/v1/orders/{code}")).Body.Data().GetProperty("status").GetString().ShouldBe("Cancelled");
        (await api.DbAsync(db => db.Set<Notification>().AnyAsync(n => n.UserId == customerId && n.Body!.Contains("Out of this size")))).ShouldBeTrue();
    }

    [Fact]
    public async Task Sellers_sell_own_products_only_in_store_mode()
    {
        var (seller, _, sellerId, handle) = await kit.SellerAsync(mode: SellerMode.PrintOnDemand);
        (await seller.GetAsync("/api/v1/seller/store/products")).Body.Code().ShouldBe("own_products_off");
        var draft = await kit.DesignAsync(sellerId, DesignStatus.Draft);
        var profile = new { name = "My Prints", handle, coverPositionY = 50, mode = "both", featuredDesignId = (long?)draft };
        (await seller.SendAsync(HttpMethod.Put, "/api/v1/seller/store", profile)).Body.Code().ShouldBe("bad_featured");
        var saved = (await seller.SendAsync(HttpMethod.Put, "/api/v1/seller/store", profile with { featuredDesignId = null })).Body.Data();
        saved.GetProperty("mode").GetString().ShouldBe("both");
        (await seller.PostAsync("/api/v1/seller/store/products", Product("Tote Bag"))).Body.Data().GetProperty("status").GetString().ShouldBe("WaitingForReview");
        (await seller.GetAsync("/api/v1/seller/store/orders")).Body.Data().GetArrayLength().ShouldBe(0);

        var breakUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)).ToString("yyyy-MM-dd");
        (await seller.SendAsync(HttpMethod.Put, "/api/v1/seller/store", new { name = "My Prints", handle, coverPositionY = 50, breakUntil, breakNote = "Back soon" }))
            .Body.Data().GetProperty("status").GetString().ShouldBe("OnBreak");
        (await (await api.ClientAsync()).GetAsync($"/api/v1/shop/products?store={handle}")).Body.Data().GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task A_sellers_product_goes_on_sale_after_review_and_a_new_name_sends_it_back()
    {
        var (seller, _, _, handle) = await kit.SellerAsync(mode: SellerMode.OwnProducts);
        var ceo = await api.CeoAsync();
        var guest = await api.ClientAsync();
        var id = (await seller.PostAsync("/api/v1/seller/store/products", Product("Shin Guards"))).Body.Data().GetProperty("id").GetInt64();
        await seller.SendFormAsync(HttpMethod.Post, $"/api/v1/seller/store/products/{id}/images", new { }, ("photos", "g.png", ShopKit.Png()));
        (await guest.GetAsync($"/api/v1/shop/products?store={handle}")).Body.Data().GetArrayLength().ShouldBe(0);
        (await ceo.GetAsync("/api/v1/admin/products/queue")).Body.Data().EnumerateArray().ShouldContain(p => p.GetProperty("id").GetInt64() == id);

        (await ceo.PostAsync($"/api/v1/admin/products/{id}/reject", new { reason = "Add a photo of the back" })).Status.ShouldBe(HttpStatusCode.OK);
        var rejected = (await seller.GetAsync($"/api/v1/seller/store/products/{id}")).Body.Data();
        (rejected.GetProperty("status").GetString(), rejected.GetProperty("reviewNote").GetString()).ShouldBe(("Rejected", "Add a photo of the back"));
        (await ceo.PostAsync($"/api/v1/admin/products/{id}/approve")).Body.Code().ShouldBe("not_waiting");
        (await seller.SendAsync(HttpMethod.Put, $"/api/v1/seller/store/products/{id}", Product("Shin Guards"))).Body.Data().GetProperty("status").GetString().ShouldBe("WaitingForReview");

        await kit.ApproveAsync(id);
        (await guest.GetAsync($"/api/v1/shop/products?store={handle}")).Body.Data().GetArrayLength().ShouldBe(1);
        var cheaper = (await seller.GetAsync($"/api/v1/seller/store/products/{id}")).Body.Data();
        var variants = cheaper.GetProperty("variants").EnumerateArray().Select(v => new { id = (long?)v.GetProperty("id").GetInt64(), size = v.GetProperty("size").GetString(),
            colorName = v.GetProperty("colorName").GetString(), colorHex = "#000000", sku = (string?)null, stock = 3, priceOverride = (decimal?)null }).ToArray<object>();
        (await seller.SendAsync(HttpMethod.Put, $"/api/v1/seller/store/products/{id}", Product("Shin Guards", variants: variants))).Body.Data().GetProperty("status").GetString().ShouldBe("Published");
        (await seller.SendAsync(HttpMethod.Put, $"/api/v1/seller/store/products/{id}", Product("Shin Guards Pro", variants: variants))).Body.Data().GetProperty("status").GetString().ShouldBe("WaitingForReview");
        (await guest.GetAsync($"/api/v1/shop/products?store={handle}")).Body.Data().GetArrayLength().ShouldBe(0);
        (await seller.GetAsync("/api/v1/admin/products/queue")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Staff_run_the_official_store_and_hide_products()
    {
        var ceo = await api.CeoAsync();
        var products = (await ceo.GetAsync("/api/v1/admin/store/products")).Body.Data();
        products.GetArrayLength().ShouldBeGreaterThanOrEqualTo(30);
        (await ceo.PostAsync("/api/v1/admin/store/products", Product("Official Cap"))).Body.Data().GetProperty("status").GetString().ShouldBe("Published");
        var (partner, handle, product) = await PartnerProductAsync();
        var id = product.GetProperty("id").GetInt64();
        (await ceo.PostAsync($"/api/v1/admin/products/{id}/hide", new { reason = "x" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.PostAsync($"/api/v1/admin/products/{id}/hide", new { reason = "Fake brand logo" })).Status.ShouldBe(HttpStatusCode.OK);
        (await (await api.ClientAsync()).GetAsync($"/api/v1/shop/products?store={handle}")).Body.Data().GetArrayLength().ShouldBe(0);
        (await partner.GetAsync($"/api/v1/partner/store/products/{id}")).Body.Data().GetProperty("hiddenReason").GetString().ShouldBe("Fake brand logo");
        (await ceo.PostAsync($"/api/v1/admin/products/{id}/unhide")).Status.ShouldBe(HttpStatusCode.OK);
        (await ceo.PostAsync($"/api/v1/admin/products/{id}/unhide")).Body.Code().ShouldBe("wrong_state");
        (await partner.PostAsync($"/api/v1/admin/products/{id}/hide", new { reason = "Fake brand logo" })).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await partner.GetAsync("/api/v1/admin/store/products")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
