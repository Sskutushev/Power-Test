namespace Weather.Web.Extensions;

/// <summary>
/// Baseline response security headers.
/// </summary>
public static class SecurityHeadersExtensions
{
    /// <summary>
    /// Content Security Policy tuned for this app rather than copied blindly.
    /// <list type="bullet">
    /// <item><description><c>script-src 'self'</c>: Blazor Server needs no inline or eval'd script.</description></item>
    /// <item><description><c>style-src 'unsafe-inline'</c>: Leaflet and the Blazor reconnect UI set style attributes.</description></item>
    /// <item><description><c>connect-src</c> allows the SignalR circuit and the keyless RainViewer radar index.</description></item>
    /// <item><description><c>img-src</c> allows provider icons plus OpenStreetMap and RainViewer tiles.</description></item>
    /// </list>
    /// </summary>
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "font-src 'self' data:; " +
        "img-src 'self' data: https://cdn.weatherapi.com https://*.tile.openstreetmap.org https://tilecache.rainviewer.com; " +
        "connect-src 'self' ws: wss: https://api.rainviewer.com";

    /// <summary>Adds the header set to every response.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            IHeaderDictionary headers = context.Response.Headers;
            headers["Content-Security-Policy"] = ContentSecurityPolicy;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            // Geolocation is allowed for this origin only, and only because the opt-in location switcher
            // needs it. Everything else stays denied outright.
            headers["Permissions-Policy"] = "geolocation=(self), camera=(), microphone=(), payment=(), usb=(), interest-cohort=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";

            await next(context);
        });
    }
}
