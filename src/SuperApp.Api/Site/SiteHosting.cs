using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace SuperApp.Api.Site;

/// <summary>
/// Serves the existing site (index.html, css, js, models, vendor…) from the repository root, unchanged, at
/// <c>/super-app/</c> — the same address as GitHub Pages and tools/serve.ps1. Only the site's own folders and
/// files are reachable; nothing else in the repository (source code, .env, docs) can be downloaded.
/// Page addresses without a file extension (e.g. /super-app/factory/jobs) return index.html (clean URLs).
/// </summary>
public static class SiteHosting
{
    public const string BasePath = "/super-app";
    private static readonly string[] Folders = ["css", "js", "models", "vendor"];
    private static readonly string[] RootFiles = ["index.html", "404.html", "og-image.png", "robots.txt", "sitemap.xml"];

    public static WebApplication UseSuperAppSite(this WebApplication app)
    {
        var root = ResolveRoot(app);
        if (root is null)
        {
            app.Logger.LogWarning("Site files not found — the API runs without the site.");
            return app;
        }
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".glb"] = "model/gltf-binary";
        types.Mappings.Remove(".py");
        foreach (var folder in Folders)
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(root, folder)),
                RequestPath = $"{BasePath}/{folder}",
                ContentTypeProvider = types,
                ServeUnknownFileTypes = false,
                // The browser checks with the server before reusing a file (a cheap 304), so updates show up at once
                OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
            });
        }
        app.MapGet("/", () => Results.Redirect($"{BasePath}/")).ExcludeFromDescription();
        app.MapGet($"{BasePath}/{{**path}}", (HttpContext http, string? path, SeoPages seo, CancellationToken ct) => ServePageAsync(http, root, path, seo, ct)).ExcludeFromDescription();
        return app;
    }

    private static async Task<IResult> ServePageAsync(HttpContext http, string root, string? path, SeoPages seo, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-cache";
        var name = string.IsNullOrEmpty(path) ? "index.html" : path;
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
        var html = page is null ? index : SeoPages.Render(index, page, $"{http.Request.Scheme}://{http.Request.Host}");
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static string ContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".png" => "image/png",
        ".xml" => "application/xml",
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
