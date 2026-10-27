using System.Net;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>With Features:StoreEnabled off (the site's setting for now) the store APIs answer 423 and everything else works.</summary>
[Trait("Category", "Database")]
public sealed class StorePausedTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    [Fact]
    public async Task The_store_answers_coming_soon_while_pitches_keep_working()
    {
        using var paused = api.WithWebHostBuilder(b => b.UseSetting("Features:StoreEnabled", "false"));
        var http = paused.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var shop = await http.GetAsync("/api/v1/shop/products");
        shop.StatusCode.ShouldBe(HttpStatusCode.Locked);
        (await shop.Content.ReadAsStringAsync()).ShouldContain("store_paused");
        (await http.GetAsync("/api/v1/pitches")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>The store sells own products while print on demand stays off (Features:PodEnabled false).</summary>
    [Fact]
    public async Task Print_on_demand_answers_paused_while_own_products_sell()
    {
        using var noPod = api.WithWebHostBuilder(b => b.UseSetting("Features:PodEnabled", "false"));
        var http = noPod.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var design = await http.GetAsync("/api/v1/shop/designs/1");
        design.StatusCode.ShouldBe(HttpStatusCode.Locked);
        (await design.Content.ReadAsStringAsync()).ShouldContain("pod_paused");
        var products = await http.GetAsync("/api/v1/shop/products?kind=design");
        products.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await products.Content.ReadAsStringAsync()).ShouldNotContain("\"kind\":\"design\"");
    }
}
