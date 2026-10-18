using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>The design studio, review, reports and strikes, and the seller dashboard (API §4.4, §7.2 Designs).</summary>
[Trait("Category", "Database")]
public sealed class SellerStudioTests(DemoApiFactory api) : IClassFixture<DemoApiFactory>
{
    private readonly ShopKit kit = new(api);

    private static object Save(JsonElement d, string title = "Five A Side", decimal price = 400, long[]? colors = null, string category = "football") => new
    {
        catalogProductId = d.GetProperty("catalogProductId").GetInt32(), categoryKey = category, title, description = "Night games.", price,
        colorIds = colors ?? [], tags = new[] { "cairo", "5aside" }, front = (object?)null, back = (object?)null, rowVersion = d.GetProperty("rowVersion").GetString(),
    };

    private static async Task<JsonElement> UploadAsync(ApiClient seller, long id, string side, int size = 600)
    {
        var r = await seller.SendFormAsync(HttpMethod.Post, $"/api/v1/seller/designs/{id}/art/{side}", new { }, ("file", "art.png", ShopKit.Png(size)));
        r.Status.ShouldBe(HttpStatusCode.OK, r.Body.ToString());
        return r.Body.Data();
    }

    private static async Task<(JsonElement Draft, long[] Colors)> DraftAsync(ApiClient seller)
    {
        var tee = (await seller.GetAsync("/api/v1/seller/catalogue")).Body.Data().EnumerateArray().Single(c => c.GetProperty("key").GetString() == "pod-tee");
        tee.TryGetProperty("factoryCost", out _).ShouldBeFalse();
        var colors = tee.GetProperty("colors").EnumerateArray().Take(2).Select(c => c.GetProperty("id").GetInt64()).ToArray();
        var draft = await seller.PostAsync("/api/v1/seller/designs", new { catalogProductId = tee.GetProperty("id").GetInt32() });
        draft.Status.ShouldBe(HttpStatusCode.Created, draft.Body.ToString());
        return (draft.Body.Data(), colors);
    }

    [Fact]
    public async Task A_design_goes_from_draft_through_review_to_the_shop()
    {
        var (seller, _, _, _) = await kit.SellerAsync();
        var (draft, colors) = await DraftAsync(seller);
        var id = draft.GetProperty("id").GetInt64();
        draft.GetProperty("price").GetDecimal().ShouldBe(300);
        (await seller.PostAsync($"/api/v1/seller/designs/{id}/submit")).Body.Code().ShouldBe("incomplete");
        var art = await UploadAsync(seller, id, "front");
        art.GetProperty("quality").GetString().ShouldBe("low");
        art.GetProperty("dpi").GetInt32().ShouldBe(51);
        var d = art.GetProperty("design");
        d.GetProperty("front").ValueKind.ShouldBe(JsonValueKind.Object);
        (await seller.SendAsync(HttpMethod.Put, $"/api/v1/seller/designs/{id}", Save(d, price: 250, colors: colors))).Body.Code().ShouldBe("price_below_base");
        (await seller.SendAsync(HttpMethod.Put, $"/api/v1/seller/designs/{id}", Save(d, price: 1000, colors: colors))).Body.Code().ShouldBe("price_above_max");
        d = (await seller.SendAsync(HttpMethod.Put, $"/api/v1/seller/designs/{id}", Save(d, colors: colors))).Body.Data();
        d.GetProperty("profit").GetDecimal().ShouldBe(100);
        (await seller.SendAsync(HttpMethod.Put, $"/api/v1/seller/designs/{id}", Save(art.GetProperty("design"), colors: colors))).Status.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Conflict);
        d = (await UploadAsync(seller, id, "back")).GetProperty("design");
        d.GetProperty("price").GetDecimal().ShouldBe(460);   // the back print adds 60, profit stays 100
        d.GetProperty("profit").GetDecimal().ShouldBe(100);

        (await seller.PostAsync($"/api/v1/seller/designs/{id}/submit")).Body.Data().GetProperty("status").GetString().ShouldBe("InReview");
        (await seller.Http.GetAsync($"/api/v1/shop/designs/{id}/art/front")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var guest = await api.ClientAsync();
        (await guest.Http.GetAsync($"/api/v1/shop/designs/{id}/art/front")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var ceo = await api.CeoAsync();
        (await ceo.GetAsync("/api/v1/admin/designs/queue?pageSize=100")).Body.Data().EnumerateArray().ShouldContain(q => q.GetProperty("id").GetInt64() == id);
        (await ceo.PostAsync($"/api/v1/admin/designs/{id}/reject", new { reason = "no" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.PostAsync($"/api/v1/admin/designs/{id}/reject", new { reason = "The logo looks like a club badge." })).Status.ShouldBe(HttpStatusCode.OK);
        d = (await seller.GetAsync($"/api/v1/seller/designs/{id}")).Body.Data();
        d.GetProperty("rejectReason").GetString().ShouldNotBeNull();
        d = (await seller.SendAsync(HttpMethod.Put, $"/api/v1/seller/designs/{id}", Save(d, price: 460, colors: colors))).Body.Data();
        d.GetProperty("status").GetString().ShouldBe("Draft");
        await seller.PostAsync($"/api/v1/seller/designs/{id}/submit");
        (await ceo.PostAsync($"/api/v1/admin/designs/{id}/approve")).Status.ShouldBe(HttpStatusCode.OK);
        (await ceo.PostAsync($"/api/v1/admin/designs/{id}/approve")).Body.Code().ShouldBe("wrong_status");
        (await guest.Http.GetAsync($"/api/v1/shop/designs/{id}/art/front")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await guest.GetAsync($"/api/v1/shop/designs/{id}")).Status.ShouldBe(HttpStatusCode.OK);

        (await seller.PostAsync($"/api/v1/seller/designs/{id}/pause")).Body.Data().GetProperty("status").GetString().ShouldBe("Paused");
        (await guest.GetAsync($"/api/v1/shop/designs/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await seller.PostAsync($"/api/v1/seller/designs/{id}/pause")).Body.Code().ShouldBe("wrong_status");
        (await seller.PostAsync($"/api/v1/seller/designs/{id}/resume")).Body.Data().GetProperty("status").GetString().ShouldBe("Live");
        (await seller.SendAsync(HttpMethod.Delete, $"/api/v1/seller/designs/{id}")).Status.ShouldBe(HttpStatusCode.OK);
        (await seller.GetAsync($"/api/v1/seller/designs/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Elite_designs_go_live_at_once_with_a_spot_check_and_stale_saves_are_refused()
    {
        var (seller, _, _, _) = await kit.SellerAsync("elite");
        var (draft, colors) = await DraftAsync(seller);
        var id = draft.GetProperty("id").GetInt64();
        draft.GetProperty("base").GetDecimal().ShouldBe(265);   // 300 − Elite discount 35
        var d = (await UploadAsync(seller, id, "front", 1200)).GetProperty("design");
        var saved = (await seller.SendAsync(HttpMethod.Put, $"/api/v1/seller/designs/{id}", Save(d, colors: colors))).Body.Data();
        await Task.Delay(1100);
        await UploadAsync(seller, id, "front", 1200);
        (await seller.SendAsync(HttpMethod.Put, $"/api/v1/seller/designs/{id}", Save(saved, colors: colors))).Body.Code().ShouldBe("stale");
        var live = (await seller.PostAsync($"/api/v1/seller/designs/{id}/submit")).Body.Data();
        live.GetProperty("status").GetString().ShouldBe("Live");
        live.GetProperty("spotCheck").GetBoolean().ShouldBeTrue();
        var ceo = await api.CeoAsync();
        var queued = (await ceo.GetAsync("/api/v1/admin/designs/queue?pageSize=100")).Body.Data().EnumerateArray().Single(q => q.GetProperty("id").GetInt64() == id);
        queued.GetProperty("lane").GetString().ShouldBe("SpotCheck");
        await ceo.PostAsync($"/api/v1/admin/designs/{id}/approve");
        (await seller.GetAsync($"/api/v1/seller/designs/{id}")).Body.Data().GetProperty("spotCheck").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Levels_limit_active_designs_and_bulk_upload()
    {
        var (starter, _, starterId, _) = await kit.SellerAsync();
        var teeId = await api.DbAsync(db => db.Set<CatalogProduct>().Where(c => c.Key == "pod-tee").Select(c => c.Id).SingleAsync());
        for (var i = 0; i < 25; i++)
        {
            await kit.DesignAsync(starterId, DesignStatus.Live, $"Limit {i}");
        }
        (await starter.PostAsync("/api/v1/seller/designs", new { catalogProductId = teeId })).Body.Code().ShouldBe("design_limit");
        var answers = new { catalogProductId = 1, categoryKey = "football", price = 400m, colorIds = Array.Empty<long>(), tags = new[] { "bulk" } };
        (await starter.PostFormAsync("/api/v1/seller/designs/bulk", answers, ("files", "a.png", ShopKit.Png()))).Body.Code().ShouldBe("bulk_level");

        var (pro, _, _, _) = await kit.SellerAsync("pro");
        var (_, colors) = await DraftAsync(pro);
        var tee = await api.DbAsync(db => db.Set<CatalogProduct>().Where(c => c.Key == "pod-tee").Select(c => c.Id).SingleAsync());
        var uploaded = await pro.PostFormAsync("/api/v1/seller/designs/bulk", answers with { catalogProductId = tee, colorIds = colors, price = 350m },
            ("files", "Cairo_Derby.png", ShopKit.Png()), ("files", "Pyramids-FC.png", ShopKit.Png()));
        uploaded.Status.ShouldBe(HttpStatusCode.Created, uploaded.Body.ToString());
        uploaded.Body.Data().EnumerateArray().Select(d => d.GetProperty("title").GetString()).ShouldBe(["Cairo Derby", "Pyramids FC"]);
        uploaded.Body.Data().EnumerateArray().ShouldAllBe(d => d.GetProperty("status").GetString() == "InReview");
    }

    [Fact]
    public async Task Taking_designs_down_with_strikes_freezes_then_suspends_the_seller()
    {
        var (seller, _, sellerId, handle) = await kit.SellerAsync();
        var ceo = await api.CeoAsync();
        for (var strike = 1; strike <= 3; strike++)
        {
            var id = await kit.DesignAsync(sellerId, DesignStatus.Live, $"Copy {strike}");
            var guest = await api.ClientAsync();
            (await guest.PostAsync($"/api/v1/shop/designs/{id}/report", new { reason = "Copyright", detail = "My drawing" })).Status.ShouldBe(HttpStatusCode.Created);
            var report = (await ceo.GetAsync("/api/v1/admin/design-reports?pageSize=100")).Body.Data().EnumerateArray().Single(r => r.GetProperty("designId").GetInt64() == id);
            (await ceo.PostAsync($"/api/v1/admin/design-reports/{report.GetProperty("id").GetInt64()}/take-down", new { reason = "Copied artwork", strike = true })).Status.ShouldBe(HttpStatusCode.OK);
            var status = await api.DbAsync(db => db.Set<Seller>().Where(s => s.Id == sellerId).Select(s => s.Status).SingleAsync());
            status.ShouldBe(strike < 3 ? SellerStatus.Frozen : SellerStatus.Suspended);
        }
        (await (await api.ClientAsync()).GetAsync($"/api/v1/stores/{handle}")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await seller.GetAsync("/api/v1/seller/overview")).Body.Code().ShouldBe("seller_suspended");

        var keep = await kit.DesignIdAsync("Ya Habibi");
        await (await api.ClientAsync()).PostAsync($"/api/v1/shop/designs/{keep}/report", new { reason = "Offensive" });
        var open = (await ceo.GetAsync("/api/v1/admin/design-reports?pageSize=100")).Body.Data().EnumerateArray().First(r => r.GetProperty("designId").GetInt64() == keep);
        (await ceo.PostAsync($"/api/v1/admin/design-reports/{open.GetProperty("id").GetInt64()}/dismiss")).Status.ShouldBe(HttpStatusCode.OK);
        (await ceo.PostAsync($"/api/v1/admin/design-reports/{open.GetProperty("id").GetInt64()}/dismiss")).Body.Code().ShouldBe("wrong_status");
    }

    [Fact]
    public async Task The_dashboard_shows_earnings_orders_samples_codes_and_analytics()
    {
        var (seller, _, sellerId, handle) = await kit.SellerAsync();
        var design = await kit.DesignAsync(sellerId, DesignStatus.Live, "Dashboard Tee", 450);
        var (customer, _) = await kit.PersonAsync();
        await customer.PostAsync("/api/v1/cart/items", new { designId = design, size = "M", color = "Black", qty = 2 });
        var code = (await ShopKit.CheckoutAsync(customer)).Body.Data().GetProperty("code").GetString()!;
        var overview = (await seller.GetAsync("/api/v1/seller/overview")).Body.Data();
        overview.GetProperty("kpis").GetProperty("pendingEarnings").GetDecimal().ShouldBe(300);   // 2 × (450 − 300)
        overview.GetProperty("level").GetProperty("nextKey").GetString().ShouldBe("rising");
        (await seller.GetAsync("/api/v1/seller/orders")).Body.Data()[0].GetProperty("factoryStage").GetString().ShouldBe("New");
        (await seller.PostAsync("/api/v1/seller/samples", new { designId = design, color = "Black", size = "M", address = "12 Test Street, Dokki, Giza" })).Body.Code().ShouldBe("balance_too_low");

        await api.DbAsync(db => db.Set<OrderItem>().Where(i => i.Order.Code == code)
            .ExecuteUpdateAsync(u => u.SetProperty(i => i.Status, OrderItemStatus.Delivered).SetProperty(i => i.DeliveredAt, DateTime.UtcNow.AddDays(-10))));
        (await seller.GetAsync("/api/v1/seller/overview")).Body.Data().GetProperty("kpis").GetProperty("availableEarnings").GetDecimal().ShouldBe(300);
        var sample = await seller.PostAsync("/api/v1/seller/samples", new { designId = design, color = "Black", size = "M", address = "12 Test Street, Dokki, Giza" });
        sample.Status.ShouldBe(HttpStatusCode.Created, sample.Body.ToString());
        sample.Body.Data().GetProperty("price").GetDecimal().ShouldBe(300);
        (await seller.GetAsync("/api/v1/seller/overview")).Body.Data().GetProperty("kpis").GetProperty("availableEarnings").GetDecimal().ShouldBe(0);

        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        object Code(string c) => new { code = c, discountType = "Percent", value = 10m, minSpend = 0m, maxUses = (int?)null, perCustomer = (int?)1, startsOn = today, endsOn = (string?)null, label = "Launch" };
        (await seller.PostAsync("/api/v1/seller/codes", Code("LAUNCH" + handle[2..6].ToUpperInvariant()))).Status.ShouldBe(HttpStatusCode.Created);
        (await seller.PostAsync("/api/v1/seller/codes", Code("SECOND" + handle[2..6].ToUpperInvariant()))).Status.ShouldBe(HttpStatusCode.Created);
        (await seller.PostAsync("/api/v1/seller/codes", Code("THIRD" + handle[2..6].ToUpperInvariant()))).Body.Code().ShouldBe("code_limit");
        (await seller.PostAsync("/api/v1/seller/codes", Code("LAUNCH" + handle[2..6].ToUpperInvariant()))).Body.Code().ShouldBe("code_taken");

        var analytics = (await seller.GetAsync("/api/v1/seller/analytics")).Body.Data();
        analytics.GetProperty("items").GetInt32().ShouldBe(2);
        analytics.GetProperty("days").GetArrayLength().ShouldBe(0);   // Basic tier: no daily chart
        (await seller.Http.GetAsync("/api/v1/seller/analytics.xlsx")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await seller.GetAsync("/api/v1/seller/calendar")).Status.ShouldBe(HttpStatusCode.OK);
        (await seller.GetAsync("/api/v1/seller/kyc")).Body.Data().GetProperty("status").GetString().ShouldBe("None");

        var (elite, _, _, _) = await kit.SellerAsync("elite");
        var xlsx = await elite.Http.GetAsync("/api/v1/seller/analytics.xlsx");
        xlsx.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (stranger, _) = await kit.PersonAsync();
        (await stranger.GetAsync("/api/v1/seller/overview")).Body.Code().ShouldBe("not_seller");
    }
}
