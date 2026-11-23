using Microsoft.AspNetCore.Http.Features;

namespace SuperApp.Api.Middlewares;

/// <summary>
/// The home page downloads large public lists (fantasy players ~300 KB, fixtures, matches, pitches). They are the same for
/// everyone and hold no secret or personal data, so they are sent compressed (about a fifth of the size on mobile data).
/// Every other API answer stays uncompressed: a compressed https answer that mixes a secret with text the caller controls
/// can leak that secret (BREACH), so only GET requests to these public catalogues are allowed through.
/// </summary>
public sealed class PublicJsonCompressionMiddleware(RequestDelegate next)
{
    private static readonly string[] PublicCatalogues =
    [
        "/api/v1/pitches", "/api/v1/matches", "/api/v1/lookups", "/api/v1/content/home", "/api/v1/help", "/api/v1/legal",
        "/api/v1/shop/products", "/api/v1/free-slots",
    ];

    private static readonly string[] PublicFantasy = ["/bootstrap", "/players", "/fixtures", "/table", "/competitions"];

    public Task InvokeAsync(HttpContext http)
    {
        var path = http.Request.Path.Value ?? "";
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) && !IsPublicCatalogue(http.Request.Method, path)
            && http.Features.Get<IHttpsCompressionFeature>() is { } feature)
        {
            feature.Mode = HttpsCompressionMode.DoNotCompress;
        }
        return next(http);
    }

    /// <summary>A GET for a public list or detail page. Fantasy: only the shared game data, never a manager's own team.</summary>
    public static bool IsPublicCatalogue(string method, string path)
    {
        if (!HttpMethods.IsGet(method))
        {
            return false;
        }
        if (path.StartsWith("/api/v1/fantasy/", StringComparison.OrdinalIgnoreCase))
        {
            return !path.Contains("/team", StringComparison.OrdinalIgnoreCase) && !path.Contains("/groups", StringComparison.OrdinalIgnoreCase)
                && PublicFantasy.Any(p => path.EndsWith(p, StringComparison.OrdinalIgnoreCase) || path.Contains(p + "/", StringComparison.OrdinalIgnoreCase));
        }
        return PublicCatalogues.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase) || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase));
    }
}
