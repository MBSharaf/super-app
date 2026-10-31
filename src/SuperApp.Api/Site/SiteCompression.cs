using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;

namespace SuperApp.Api.Site;

/// <summary>
/// Compresses the site's files (pages, vendor scripts and styles, the 3D model, manifest…) with Brotli or gzip.
/// Without it the site would go out about three times bigger than on GitHub Pages (performance report, docs/perf).
/// Only addresses under <c>/super-app</c> are compressed: API answers can hold tokens and personal data next to text
/// a caller controls, which is how BREACH-style attacks guess secrets from compressed sizes over https.
/// Bundle files arrive already compressed (<see cref="SiteBundle"/>) and are left as they are.
/// </summary>
public static class SiteCompression
{
    public static IServiceCollection AddSiteCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(o =>
        {
            o.EnableForHttps = true;   // safe here: only the public site files above, never /api
            o.Providers.Add<BrotliCompressionProvider>();
            o.Providers.Add<GzipCompressionProvider>();
            o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["image/svg+xml", "model/gltf-binary", "application/manifest+json"]);
        });
        services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
        return services;
    }

    public static WebApplication UseSiteCompression(this WebApplication app)
    {
        app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments(SiteHosting.BasePath), site => site.UseResponseCompression());
        return app;
    }
}
