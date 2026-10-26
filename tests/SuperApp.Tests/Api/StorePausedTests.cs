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
}
