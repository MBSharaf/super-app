using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
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
    [InlineData("/llms.txt", "text/plain")]
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

    [Theory]
    [InlineData("/pitches?x=1", "https://supermalaab.com/pitches?x=1")]
    [InlineData("/", "https://supermalaab.com/")]
    public async Task The_www_address_moves_to_the_main_domain_for_good(string path, string to)
    {
        var www = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://www.supermalaab.com") });
        var response = await www.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.MovedPermanently, path);
        response.Headers.Location!.OriginalString.ShouldBe(to);
    }

    [Fact]
    public async Task Home_page_tells_search_engines_both_names_of_the_brand()
    {
        var html = await (await Client().GetAsync("/")).Content.ReadAsStringAsync();
        html.ShouldContain("<title>سوبر ملعب Super Malaab");
        html.ShouldContain("\"@type\":\"Organization\"");
        html.ShouldContain("alternateName");
        html.ShouldContain("\"sameAs\":[\"https://www.facebook.com/share/1A4odKYei2/\"");
        html.ShouldContain("<h1>سوبر ملعب Super Malaab");
        html.ShouldNotContain("Egyptian Premier League fantasy");
    }

    [Theory]
    [InlineData("/players", "Find players")]
    [InlineData("/fantasy", "Fantasy")]
    [InlineData("/help", "Help")]
    public async Task Public_pages_have_their_own_title(string path, string title)
    {
        var html = await (await Client().GetAsync(path)).Content.ReadAsStringAsync();
        html.ShouldContain($"| {title} — Super Malaab سوبر ملعب</title>");
        html.ShouldContain($"<link rel=\"canonical\" href=\"https://localhost{path}\" />");
    }

    [Theory]
    [InlineData("GET", "/api/v1/fantasy/epl/bootstrap", true)]
    [InlineData("GET", "/api/v1/pitches", true)]
    [InlineData("GET", "/api/v1/pitches/zamalek/availability", true)]
    [InlineData("GET", "/api/v1/fantasy/epl/team", false)]
    [InlineData("GET", "/api/v1/auth/csrf", false)]
    [InlineData("GET", "/api/v1/me", false)]
    [InlineData("POST", "/api/v1/pitches", false)]
    [InlineData("GET", "/api/v1/pitchesx", false)]
    public void Only_public_catalogue_answers_are_compressed(string method, string path, bool compressed) =>
        SuperApp.Api.Middlewares.PublicJsonCompressionMiddleware.IsPublicCatalogue(method, path).ShouldBe(compressed);

    [Fact]
    public async Task Pages_load_one_joined_script_and_one_joined_style_file_kept_for_a_year()
    {
        (await api.Services.GetRequiredService<SuperApp.Api.Site.SiteBundles>().Ready).ShouldBeTrue();
        var html = await (await Client().GetAsync("/pitches")).Content.ReadAsStringAsync();
        html.ShouldNotContain("src=\"js/app.js\"");
        html.ShouldNotContain("href=\"css/tokens.css\"");
        html.ShouldContain("src=\"vendor/leaflet/leaflet.js\"");
        var js = System.Text.RegularExpressions.Regex.Match(html, "src=\"(js/bundle-[0-9a-f]+\\.js)\"").Groups[1].Value;
        var css = System.Text.RegularExpressions.Regex.Match(html, "href=\"(css/bundle-[0-9a-f]+\\.css)\"").Groups[1].Value;
        js.ShouldNotBeEmpty();
        css.ShouldNotBeEmpty();
        var request = new HttpRequestMessage(HttpMethod.Get, "/" + js);
        request.Headers.AcceptEncoding.ParseAdd("br");
        var response = await Client().SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentEncoding.ShouldContain("br");
        response.Headers.CacheControl!.ToString().ShouldContain("immutable");
        var plain = await Client().GetStringAsync("/" + css);
        plain.ShouldContain("/* css/tokens.css */");
        (await Client().GetAsync("/js/bundle-000000000000.js")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/** doc */\n(function (SA) {\n  const x = 1;\n})(window.SA);\n", true)]
    [InlineData("// note\n(() => { go(); })();", true)]
    [InlineData("const SA = { a: 1 };\n(function () {})();", false)]
    public void Only_self_contained_scripts_get_their_own_try_catch(string code, bool wrapped) =>
        SuperApp.Api.Site.SiteBundles.IsWrapped(code).ShouldBe(wrapped);
}
