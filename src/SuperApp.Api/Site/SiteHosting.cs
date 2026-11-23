using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using SuperApp.Application.Features.Content;

namespace SuperApp.Api.Site;

/// <summary>
/// Serves the existing site (index.html, css, js, models, vendor…) from the repository root, unchanged, at
/// <c>/super-app/</c> — the same address as GitHub Pages and tools/serve.ps1. Only the site's own folders and
/// files are reachable; nothing else in the repository (source code, .env, docs) can be downloaded.
/// Page addresses without a file extension (e.g. /super-app/factory/jobs) return index.html (clean URLs).
/// The page tells the site which parts are open: <c>&lt;html data-store="on|off" data-pod="on|off"&gt;</c> from the settings
/// <c>Features:StoreEnabled</c> and <c>Features:PodEnabled</c> (no script needed; GitHub Pages keeps both off).
/// On a real domain set <c>Site:BasePath</c> to empty: the site is then served at the domain root (supermalaab.com/pitches)
/// and every old /super-app/… address answers with a permanent redirect to the same page without the folder.
/// </summary>
public static class SiteHosting
{
    /// <summary>The folder the site lives in when Site:BasePath is not set (local runs, tests, GitHub Pages).</summary>
    public const string DefaultBasePath = "/super-app";
    private const string LegacyBasePath = "/super-app";
    private static readonly string[] Folders = ["css", "js", "models", "vendor", "icons"];
    private static readonly string[] RootFiles = ["index.html", "404.html", "og-image.png", "robots.txt", "sitemap.xml", "manifest.json", "sw.js", "offline.html", "llms.txt"];

    public static WebApplication UseSuperAppSite(this WebApplication app)
    {
        var root = ResolveRoot(app);
        if (root is null)
        {
            app.Logger.LogWarning("Site files not found — the API runs without the site.");
            return app;
        }
        var basePath = BasePathOf(app.Configuration);
        RedirectWwwToMainDomain(app);
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".glb"] = "model/gltf-binary";
        types.Mappings.Remove(".py");
        foreach (var folder in Folders)
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(root, folder)),
                RequestPath = $"{basePath}/{folder}",
                ContentTypeProvider = types,
                ServeUnknownFileTypes = false,
                // The browser checks with the server before reusing a file (a cheap 304), so updates show up at once
                OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
            });
        }
        // Search engines read these two only from the domain root; both are built from live data (ContentService)
        app.MapGet("/robots.txt", (IContentService content) => Results.Text(content.Robots(), "text/plain; charset=utf-8")).ExcludeFromDescription();
        app.MapGet("/sitemap.xml", async (IContentService content, CancellationToken ct) => Results.Text(await content.SitemapAsync(ct), "application/xml; charset=utf-8")).ExcludeFromDescription();
        if (basePath.Length == 0)
        {
            // The site is at the domain root: old /super-app/… links (emails, shares, search results) move there for good
            app.MapGet($"{LegacyBasePath}/{{**path}}", (HttpContext http, string? path) =>
                Results.Redirect($"/{path}{http.Request.QueryString}", permanent: true)).ExcludeFromDescription();
            app.MapGet(LegacyBasePath, () => Results.Redirect("/", permanent: true)).ExcludeFromDescription();
            app.MapGet("/{**path}", (HttpContext http, string? path, SeoPages seo, CancellationToken ct) => ServePageAsync(http, root, basePath, path, seo, ct)).ExcludeFromDescription();
            return app;
        }
        app.MapGet("/", () => Results.Redirect($"{basePath}/")).ExcludeFromDescription();
        app.MapGet($"{basePath}/{{**path}}", (HttpContext http, string? path, SeoPages seo, CancellationToken ct) => ServePageAsync(http, root, basePath, path, seo, ct)).ExcludeFromDescription();
        return app;
    }

    /// <summary>
    /// www.supermalaab.com and supermalaab.com showed the same pages, so search engines saw two copies of the site.
    /// When Site:PublicUrl is set, the www address answers with a permanent redirect to the same page on the main domain.
    /// </summary>
    private static void RedirectWwwToMainDomain(WebApplication app)
    {
        if (!Uri.TryCreate(app.Configuration["Site:PublicUrl"], UriKind.Absolute, out var main) || main.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var www = "www." + main.Host;
        app.Use((http, next) =>
        {
            if (!string.Equals(http.Request.Host.Host, www, StringComparison.OrdinalIgnoreCase))
            {
                return next(http);
            }
            http.Response.StatusCode = StatusCodes.Status301MovedPermanently;
            http.Response.Headers.Location = $"{main.Scheme}://{main.Authority}{http.Request.PathBase}{http.Request.Path}{http.Request.QueryString}";
            return Task.CompletedTask;
        });
    }

    /// <summary>Site:BasePath without the trailing slash: "" for the domain root, "/super-app" by default.</summary>
    public static string BasePathOf(IConfiguration configuration)
    {
        var value = configuration["Site:BasePath"];
        return value is null ? DefaultBasePath : value.Trim().TrimEnd('/') is { Length: > 0 } b ? "/" + b.TrimStart('/') : "";
    }

    private static async Task<IResult> ServePageAsync(HttpContext http, string root, string basePath, string? path, SeoPages seo, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-cache";
        var name = string.IsNullOrEmpty(path) ? "index.html" : path;
        // Server addresses (API, live hubs) never fall back to the page
        if (name.StartsWith("api/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("hubs/", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound();
        }
        if (name is "robots.txt" or "sitemap.xml")
        {
            return Results.Redirect($"/{name}", permanent: true);
        }
        if (RootFiles.Contains(name, StringComparer.OrdinalIgnoreCase) && name != "index.html")
        {
            return Results.File(Path.Combine(root, name), ContentType(name));
        }
        // A page address (no extension) opens the app; anything else that is not a site file does not exist
        if (Path.HasExtension(name) && name != "index.html")
        {
            return Results.NotFound();
        }
        var index = await File.ReadAllTextAsync(Path.Combine(root, "index.html"), ct);
        // Main public pages carry their own title, tags and readable HTML for search engines (SeoPages)
        SeoPage? page = null;
        try
        {
            page = await seo.BuildAsync(name == "index.html" ? "" : name, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The site never breaks because of search-engine extras: without them the page is served as before
            http.RequestServices.GetRequiredService<ILogger<SeoPages>>().LogWarning(ex, "SEO content skipped for {Path}", name);
        }
        index = index.Replace($"<base href=\"{DefaultBasePath}/\" />", $"<base href=\"{basePath}/\" />", StringComparison.Ordinal);
        var html = page is null ? index : SeoPages.Render(index, page, $"{http.Request.Scheme}://{http.Request.Host}{basePath}");
        return Results.Content(WithFeatures(html, http.RequestServices.GetRequiredService<IConfiguration>()), "text/html; charset=utf-8");
    }

    /// <summary>Marks the &lt;html&gt; tag with the store, print-on-demand, tournament-registration, store-payment, club-logo and fantasy switches (the demo-site closures in namespace.js never apply here).</summary>
    public static string WithFeatures(string html, IConfiguration configuration)
    {
        static string OnOff(bool on) => on ? "on" : "off";
        var store = OnOff(configuration.GetValue("Features:StoreEnabled", false));
        var pod = OnOff(configuration.GetValue("Features:PodEnabled", false));
        var tournaments = configuration.GetValue("Features:TournamentsOpen", false) ? "open" : "soon";
        var logos = OnOff(configuration.GetValue("Features:ClubLogos", true));
        var fantasy = OnOff(configuration.GetValue("Features:FantasyEnabled", true));
        var shopPay = (configuration["Features:ShopPayment"] ?? "online").Trim().ToLowerInvariant() switch { "cod" => "cod", "both" => "both", _ => "online" };
        return html.Replace("<html lang=\"en\">",
            $"<html lang=\"en\" data-store=\"{store}\" data-pod=\"{pod}\" data-tournaments=\"{tournaments}\" data-shop-pay=\"{shopPay}\" data-logos=\"{logos}\" data-fantasy=\"{fantasy}\" data-server=\"on\">", StringComparison.Ordinal);
    }

    private static string ContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".png" => "image/png",
        ".xml" => "application/xml",
        ".json" => "application/manifest+json",
        ".js" => "text/javascript; charset=utf-8",
        _ => "text/plain; charset=utf-8",
    };

    /// <summary>The folder that holds index.html: Site:Root in settings, else the repository root above src/.</summary>
    private static string? ResolveRoot(WebApplication app)
    {
        var configured = app.Configuration["Site:Root"];
        var candidates = new[]
        {
            configured is null ? null : Path.GetFullPath(configured, app.Environment.ContentRootPath),
            Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..")),
            app.Environment.WebRootPath,
        };
        return candidates.FirstOrDefault(c => c is not null && File.Exists(Path.Combine(c, "index.html")));
    }
}
