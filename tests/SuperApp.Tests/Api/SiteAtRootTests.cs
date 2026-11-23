using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>The live server (Site:BasePath = "/"): the site sits at the domain root.</summary>
public sealed class RootSiteApiFactory : AccountsApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Site:BasePath", "/");
        builder.UseSetting("Site:PublicUrl", "https://supermalaab.com/");
    }
}

/// <summary>
/// Owner's rule (2026-11-23): on supermalaab.com the address has no /super-app folder. Pages, files and search-engine
/// tags use the root, and every old /super-app/… link moves there with a permanent redirect (query kept).
/// </summary>
[Trait("Category", "Database")]
public sealed class SiteAtRootTests(RootSiteApiFactory api) : IClassFixture<RootSiteApiFactory>
{
    private HttpClient Client() => api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    [Theory]
    [InlineData("/")]
    [InlineData("/pitches")]
    [InlineData("/dash/pitches")]
    public async Task Pages_open_at_the_root_with_a_root_base(string path)
    {
        var response = await Client().GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("<base href=\"/\" />");
        html.ShouldNotContain("href=\"/super-app");
        html.ShouldNotContain("/super-app/\" />");
    }

    [Theory]
    [InlineData("/manifest.json", "application/manifest+json")]
    [InlineData("/sw.js", "text/javascript")]
    [InlineData("/icons/icon-192.png", "image/png")]
    [InlineData("/robots.txt", "text/plain")]
    public async Task Site_files_are_served_at_the_root(string path, string type)
    {
        var response = await Client().GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(type);
    }

    [Theory]
    [InlineData("/super-app/", "/")]
    [InlineData("/super-app", "/")]
    [InlineData("/super-app/pitches/p-zamalek?x=1", "/pitches/p-zamalek?x=1")]
    [InlineData("/super-app/css/app.css", "/css/app.css")]
    public async Task Old_links_move_to_the_root_for_good(string from, string to)
    {
        var response = await Client().GetAsync(from);
        response.StatusCode.ShouldBe(HttpStatusCode.MovedPermanently, from);
        response.Headers.Location!.OriginalString.ShouldBe(to);
    }

    [Theory]
    [InlineData("/.env")]
    [InlineData("/CLAUDE.md")]
    [InlineData("/hubs/unknown")]
    public async Task Other_files_and_server_addresses_are_not_pages(string path) =>
        (await Client().GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.NotFound, path);

    [Fact]
    public async Task Robots_and_sitemap_use_root_addresses()
    {
        var robots = await (await Client().GetAsync("/robots.txt")).Content.ReadAsStringAsync();
        robots.ShouldContain("Disallow: /admin\n");
        robots.ShouldNotContain("super-app");
        var sitemap = await (await Client().GetAsync("/sitemap.xml")).Content.ReadAsStringAsync();
        sitemap.ShouldContain("<loc>https://supermalaab.com/pitches</loc>");
        sitemap.ShouldNotContain("super-app");
    }
}
