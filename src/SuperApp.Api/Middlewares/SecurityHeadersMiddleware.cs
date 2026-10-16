namespace SuperApp.Api.Middlewares;

/// <summary>
/// Security headers on every response (PRD NFR-SEC-05). The content security policy allows the CDNs the site
/// already uses (fonts, mermaid-free pages, OpenStreetMap tiles, product photos).
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "img-src 'self' data: blob: https:; " +
        "connect-src 'self' https:; " +
        "media-src 'self' blob: data:; " +
        "worker-src 'self' blob:; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(self), geolocation=(self), microphone=()";
        if (!context.Request.Path.StartsWithSegments("/docs") && !context.Request.Path.StartsWithSegments("/openapi"))
        {
            headers["Content-Security-Policy"] = ContentSecurityPolicy;
        }
        return next(context);
    }
}
