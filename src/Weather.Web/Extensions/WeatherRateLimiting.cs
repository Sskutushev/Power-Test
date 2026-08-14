using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Weather.Web.Extensions;

/// <summary>Configurable limits for the public HTTP API.</summary>
public sealed class ApiRateLimitOptions
{
    /// <summary>Requests allowed per client per window.</summary>
    [Range(1, 100000)]
    public int PermitLimit { get; init; } = 30;

    /// <summary>Length of the fixed window.</summary>
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Rate limiting for the public HTTP API. The Blazor circuit is intentionally not limited: its traffic
/// is one long-lived SignalR connection per user, and limiting it would drop live UI updates.
/// </summary>
public static class WeatherRateLimiting
{
    /// <summary>Policy name applied to <c>/api/weather*</c>.</summary>
    public const string ApiPolicy = "weather-api";

    /// <summary>
    /// Configures a per-client fixed window and a spec-compliant 429 rejection. Limits are read from the
    /// request's service provider rather than captured at startup, so the bound options are the ones the
    /// final configuration produced.
    /// </summary>
    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy(ApiPolicy, context =>
        {
            ApiRateLimitOptions limits = Resolve(context);

            return RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.PermitLimit,
                    Window = limits.Window,
                    QueueLimit = 0
                });
        });

        options.OnRejected = static async (context, cancellationToken) =>
        {
            TimeSpan retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan value)
                ? value
                : Resolve(context.HttpContext).Window;

            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

            await Results
                .Problem(
                    title: "Too many requests",
                    detail: "Rate limit exceeded. Retry after the interval in the Retry-After header.",
                    statusCode: StatusCodes.Status429TooManyRequests,
                    extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["traceId"] = context.HttpContext.TraceIdentifier
                    })
                .ExecuteAsync(context.HttpContext)
                .WaitAsync(cancellationToken);
        };
    }

    private static ApiRateLimitOptions Resolve(HttpContext context)
    {
        return context.RequestServices.GetRequiredService<IOptions<ApiRateLimitOptions>>().Value;
    }
}
