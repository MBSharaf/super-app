using System.Net;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>The site at /super-app/: the app-install files (8.7) are served with the right types; the rest of the repository is not.</summary>
[Trait("Category", "Database")]
public sealed class SiteHostingTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    [Theory]
    [InlineData("/super-app/manifest.json", "application/manifest+json")]
    [InlineData("/super-app/sw.js", "text/javascript")]
    [InlineData("/super-app/offline.html", "text/html")]
    [InlineData("/super-app/icons/icon-192.png", "image/png")]
    [InlineData("/super-app/icons/icon-maskable-512.png", "image/png")]
    public async Task The_app_install_files_are_served(string path, string type)
    {
        var client = await api.ClientAsync();
        var response = await client.Http.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(type);
    }

    [Theory]
    [InlineData("/super-app/.env")]
    [InlineData("/super-app/tools/pwa/icons.py")]
    [InlineData("/super-app/CLAUDE.md")]
    public async Task Other_repository_files_are_not(string path)
    {
        var client = await api.ClientAsync();
        (await client.Http.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.NotFound, path);
    }
}
