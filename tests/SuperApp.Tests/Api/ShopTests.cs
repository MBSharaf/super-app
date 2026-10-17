using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>The public shop on the demo data (API §4.1): one list of products and designs, item pages, reports and store pages.</summary>
[Trait("Category", "Database")]
public sealed class ShopTests(DemoApiFactory api) : IClassFixture<DemoApiFactory>
{
    private readonly ShopKit kit = new(api);

    [Fact]
    public async Task The_list_has_products_and_live_designs_with_filters_and_sorting()
    {
        var guest = await api.ClientAsync();
        var all = await guest.GetAsync("/api/v1/shop/products?pageSize=60");
        all.Status.ShouldBe(HttpStatusCode.OK, all.Body.ToString());
        all.Body.GetProperty("meta").GetProperty("total").GetInt32().ShouldBeGreaterThanOrEqualTo(45);
        var items = all.Body.Data().EnumerateArray().ToList();
        items.ShouldContain(i => i.GetProperty("kind").GetString() == "design");
        items.ShouldNotContain(i => i.GetProperty("name").GetString()!.StartsWith("Press Start Hoodie"));

        var boots = (await guest.GetAsync("/api/v1/shop/products?category=boots")).Body.Data().EnumerateArray().ToList();
        boots.Count.ShouldBe(5);
        boots.ShouldAllBe(b => b.GetProperty("kind").GetString() == "product" && b.GetProperty("store").GetProperty("type").GetString() == "Official");
        (await guest.GetAsync("/api/v1/shop/products?kind=design&designCategory=football&store=koora-ink")).Body.Data().GetArrayLength().ShouldBe(3);
        (await guest.GetAsync("/api/v1/shop/products?store=turf-22-pro-shop")).Body.Data().GetArrayLength().ShouldBe(3);
        var tagged = (await guest.GetAsync("/api/v1/shop/products?tag=cairo")).Body.Data();
        tagged.EnumerateArray().ShouldContain(i => i.GetProperty("name").GetString()!.StartsWith("Cairo Nights FC"));

        var cheap = (await guest.GetAsync("/api/v1/shop/products?category=balls&sort=price")).Body.Data().EnumerateArray().Select(i => i.GetProperty("price").GetDecimal()).ToList();
        cheap.ShouldBe(cheap.OrderBy(p => p).ToList());
        (await guest.GetAsync("/api/v1/shop/products?maxPrice=300")).Body.Data().EnumerateArray().ShouldAllBe(i => i.GetProperty("price").GetDecimal() <= 300);
        (await guest.GetAsync("/api/v1/shop/products?sort=cheapest")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await guest.GetAsync("/api/v1/shop/products?minPrice=500&maxPrice=100")).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_product_page_shows_variants_and_hidden_products_answer_404()
    {
        var guest = await api.ClientAsync();
        var page = await guest.GetAsync("/api/v1/shop/products/sh-predator");
        page.Status.ShouldBe(HttpStatusCode.OK, page.Body.ToString());
        var product = page.Body.Data();
        product.GetProperty("variants").GetArrayLength().ShouldBeGreaterThan(0);
        product.GetProperty("card").GetProperty("store").GetProperty("handle").GetString().ShouldBe("super-app-store");
        product.GetProperty("images")[0].GetProperty("key").GetString().ShouldNotBeNullOrEmpty();
        (await guest.GetAsync("/api/v1/shop/products/no-such-thing")).Status.ShouldBe(HttpStatusCode.NotFound);

        await api.DbAsync(db => db.Set<Product>().Where(p => p.Slug == "sh-socks").ExecuteUpdateAsync(u => u.SetProperty(p => p.HiddenByAdmin, true).SetProperty(p => p.HiddenReason, "test")));
        (await guest.GetAsync("/api/v1/shop/products/sh-socks")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await guest.GetAsync("/api/v1/shop/products?q=socks")).Body.Data().EnumerateArray().ShouldNotContain(i => i.GetProperty("id").GetString() == "sh-socks");
        await api.DbAsync(db => db.Set<Product>().Where(p => p.Slug == "sh-socks").ExecuteUpdateAsync(u => u.SetProperty(p => p.HiddenByAdmin, false).SetProperty(p => p.HiddenReason, (string?)null)));
    }

    [Fact]
    public async Task The_design_page_counts_views_and_paused_printing_hides_designs()
    {
        var guest = await api.ClientAsync();
        var id = await kit.DesignIdAsync("Cairo Nights FC");
        var views = await api.DbAsync(db => db.Set<Design>().Where(d => d.Id == id).Select(d => d.Views).SingleAsync());
        var design = (await guest.GetAsync($"/api/v1/shop/designs/{id}")).Body.Data();
        design.GetProperty("colors").GetArrayLength().ShouldBe(2);
        design.GetProperty("sizes").GetArrayLength().ShouldBe(6);
        design.GetProperty("frontArt").GetProperty("key").GetString().ShouldBe("seed:nights");
        design.GetProperty("sellerLevel").GetString().ShouldBe("elite");
        (await api.DbAsync(db => db.Set<Design>().Where(d => d.Id == id).Select(d => d.Views).SingleAsync())).ShouldBe(views + 1);
        (await guest.GetAsync($"/api/v1/shop/designs/{await kit.DesignIdAsync("Press Start Hoodie")}")).Status.ShouldBe(HttpStatusCode.NotFound);

        await api.DbAsync(db => db.Set<Factory>().ExecuteUpdateAsync(u => u.SetProperty(f => f.IntakePaused, true)));
        try
        {
            (await guest.GetAsync("/api/v1/shop/products?kind=design")).Body.Data().GetArrayLength().ShouldBe(0);
            (await guest.GetAsync($"/api/v1/shop/designs/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
        }
        finally
        {
            await api.DbAsync(db => db.Set<Factory>().ExecuteUpdateAsync(u => u.SetProperty(f => f.IntakePaused, false)));
        }
    }

    [Fact]
    public async Task Anyone_can_report_a_live_design_once()
    {
        var id = await kit.DesignIdAsync("GG No Re");
        var guest = await api.ClientAsync();
        (await guest.PostAsync($"/api/v1/shop/designs/{id}/report", new { reason = "Trademark", detail = "Looks like a game logo" })).Status.ShouldBe(HttpStatusCode.Created);
        var (person, _) = await kit.PersonAsync();
        (await person.PostAsync($"/api/v1/shop/designs/{id}/report", new { reason = "copyright" })).Status.ShouldBe(HttpStatusCode.Created);
        (await person.PostAsync($"/api/v1/shop/designs/{id}/report", new { reason = "copyright" })).Body.Code().ShouldBe("already_reported");
        (await person.PostAsync($"/api/v1/shop/designs/{id}/report", new { reason = "Nope" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await person.PostAsync($"/api/v1/shop/designs/{id}/report", new { reason = "Other" })).Body.HasFieldError("detail").ShouldBeTrue();
        (await person.PostAsync($"/api/v1/shop/designs/{await kit.DesignIdAsync("Press Start Hoodie")}/report", new { reason = "Offensive" })).Status.ShouldBe(HttpStatusCode.NotFound);
        (await api.DbAsync(db => db.Set<DesignReport>().CountAsync(r => r.DesignId == id && r.Status == DesignReportStatus.Open))).ShouldBe(2);
    }

    [Fact]
    public async Task Store_pages_show_their_items_and_creators_can_be_followed()
    {
        var (person, _) = await kit.PersonAsync();
        var store = (await person.GetAsync("/api/v1/stores/koora-ink")).Body.Data();
        store.GetProperty("type").GetString().ShouldBe("Seller");
        store.GetProperty("level").GetString().ShouldBe("elite");
        store.GetProperty("designs").GetArrayLength().ShouldBe(4);
        store.GetProperty("isOwner").GetBoolean().ShouldBeFalse();

        var followed = await person.PostAsync("/api/v1/stores/koora-ink/follow");
        followed.Body.Data().GetProperty("followers").GetInt32().ShouldBe(1);
        (await person.GetAsync("/api/v1/stores/koora-ink")).Body.Data().GetProperty("isFollowing").GetBoolean().ShouldBeTrue();
        (await person.SendAsync(HttpMethod.Delete, "/api/v1/stores/koora-ink/follow")).Body.Data().GetProperty("followers").GetInt32().ShouldBe(0);
        (await person.PostAsync("/api/v1/stores/super-app-store/follow")).Body.Code().ShouldBe("not_followable");
        (await person.GetAsync("/api/v1/stores/nobody-here")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await (await api.ClientAsync()).PostAsync("/api/v1/stores/koora-ink/follow")).Status.ShouldBe(HttpStatusCode.Unauthorized);

        var (seller, _, sellerId, handle) = await kit.SellerAsync();
        await kit.DesignAsync(sellerId, DesignStatus.Draft, "My Draft");
        var mine = (await seller.GetAsync($"/api/v1/stores/{handle}")).Body.Data();
        mine.GetProperty("isOwner").GetBoolean().ShouldBeTrue();
        mine.GetProperty("designs")[0].GetProperty("status").GetString().ShouldBe("Draft");
        (await person.GetAsync($"/api/v1/stores/{handle}")).Body.Data().GetProperty("designs").GetArrayLength().ShouldBe(0);
        (await seller.PostAsync($"/api/v1/stores/{handle}/follow")).Body.Code().ShouldBe("own_store");
    }
}
