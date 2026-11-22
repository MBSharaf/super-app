namespace SuperApp.Api.Middlewares;

/// <summary>
/// Security headers on every response (PRD NFR-SEC-05). The content security policy allows the CDNs the site
/// already uses (fonts, mermaid-free pages, OpenStreetMap tiles, product photos) and the Google and Facebook sign-in scripts.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://accounts.google.com https://connect.facebook.net; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://accounts.google.com; " +
        "frame-src https://accounts.google.com https://www.facebook.com https://web.facebook.com; " +
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "img-src 'self' data: blob: https:; " +
        "connect-src 'self' https:; " +
        "media-src 'self' blob: data:; " +
        "worker-src 'self' blob:; " +
        "object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; upgrade-insecure-requests";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(self), geolocation=(self), microphone=(), payment=(), usb=(), interest-cohort=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin-allow-popups";   // Google and Facebook sign-in open a pop-up
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";
        if (!context.Request.Path.StartsWithSegments("/docs") && !context.Request.Path.StartsWithSegments("/openapi"))
        {
            headers["Content-Security-Policy"] = ContentSecurityPolicy;
        }
        return next(context);
    }
}
