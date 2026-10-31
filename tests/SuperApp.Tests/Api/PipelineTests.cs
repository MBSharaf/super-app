using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Shouldly;
using SuperApp.Application.Common.Responses;

namespace SuperApp.Tests.Api;

/// <summary>The request pipeline: health, unified 404, security headers, correlation id and the site files.</summary>
public sealed class PipelineTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Health_is_up_without_the_database()
    {
        var response = await client.GetAsync("/health");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unknown_api_address_answers_in_the_unified_shape()
    {
        var response = await client.GetAsync("/api/v1/does-not-exist");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        body.ShouldNotBeNull();
        body.Success.ShouldBeFalse();
        body.Errors!["code"].ShouldContain("not_found");
        body.TraceId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Every_response_carries_security_headers_and_a_correlation_id()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "abc-123");
        var response = await client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").ShouldContain("abc-123");
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
        response.Headers.Contains("Content-Security-Policy").ShouldBeTrue();
    }

    [Theory]
    [InlineData("/super-app/")]
    [InlineData("/super-app/pitches/turf-22")]
    [InlineData("/super-app/js/core/router.js")]
    [InlineData("/super-app/models/tee.glb")]
    public async Task Site_pages_and_files_are_served(string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/super-app/.env")]
    [InlineData("/super-app/.env.example")]
    [InlineData("/super-app/src/SuperApp.Api/Program.cs")]
    [InlineData("/super-app/docs/backend/PRD.md")]
    [InlineData("/super-app/models/source/trellis.py")]
    [InlineData("/super-app/js/../.env")]
    public async Task Anything_outside_the_site_folders_is_not_reachable(string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Root_redirects_to_the_site()
    {
        var response = await client.GetAsync("/");
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/super-app/");
    }

    [Fact]
    public async Task Scripts_and_styles_are_served_as_one_cached_bundle_each()
    {
        var html = await client.GetStringAsync("/super-app/shop");
        html.ShouldNotContain("src=\"js/core/router.js\"");
        html.ShouldNotContain("href=\"css/tokens.css\"");
        var js = Regex.Match(html, "<script src=\"(bundle/app\\.[0-9a-f]{12}\\.js)\" defer>").Groups[1].Value;
        var css = Regex.Match(html, "<link rel=\"stylesheet\" href=\"(bundle/app\\.[0-9a-f]{12}\\.css)\" />").Groups[1].Value;
        js.ShouldNotBeNullOrEmpty();
        css.ShouldNotBeNullOrEmpty();

        foreach (var (file, encoding) in new[] { (js, "br"), (css, "gzip") })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/super-app/{file}");
            request.Headers.Add("Accept-Encoding", encoding);
            var response = await client.SendAsync(request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, file);
            response.Content.Headers.ContentEncoding.ShouldBe([encoding]);
            response.Headers.CacheControl!.ToString().ShouldContain("immutable");
        }
    }

    [Fact]
    public async Task An_unknown_bundle_file_is_not_found()
    {
        (await client.GetAsync("/super-app/bundle/app.000000000000.js")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Site_files_are_compressed_but_api_answers_are_not()
    {
        using var site = new HttpRequestMessage(HttpMethod.Get, "/super-app/vendor/leaflet/leaflet.js");
        site.Headers.Add("Accept-Encoding", "gzip");
        (await client.SendAsync(site)).Content.Headers.ContentEncoding.ShouldBe(["gzip"]);

        using var api = new HttpRequestMessage(HttpMethod.Get, "/api/v1/does-not-exist");
        api.Headers.Add("Accept-Encoding", "gzip, br");
        (await client.SendAsync(api)).Content.Headers.ContentEncoding.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("url(images/layers.png)", "vendor/leaflet", "url(\"/super-app/vendor/leaflet/images/layers.png\")")]
    [InlineData("url('../../icons/a.svg')", "css/pages", "url(\"/super-app/icons/a.svg\")")]
    [InlineData("url(\"data:image/svg+xml,%3Csvg%3E\")", "css", "url(\"data:image/svg+xml,%3Csvg%3E\")")]
    [InlineData("url(#clip)", "css", "url(#clip)")]
    public void Style_sheet_addresses_still_point_to_the_same_file_from_the_bundle(string css, string folder, string expected)
    {
        SuperApp.Api.Site.SiteBundle.RewriteUrls(css, folder, "/super-app").ShouldBe(expected);
    }
}
