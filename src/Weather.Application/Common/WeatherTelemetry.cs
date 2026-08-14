using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Weather.Application.Common;

/// <summary>
/// Application-owned metrics and activity source. Exporter wiring lives in the host so the
/// Application layer stays free of vendor and transport concerns.
/// </summary>
public static class WeatherTelemetry
{
    /// <summary>Meter name registered with OpenTelemetry by the host.</summary>
    public const string MeterName = "WeatherApp";

    /// <summary>Activity source name registered with OpenTelemetry by the host.</summary>
    public const string ActivitySourceName = "WeatherApp";

    private static readonly Meter Meter = new(MeterName);

    /// <summary>Activity source for provider and cache spans.</summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>Duration of weather dashboard queries.</summary>
    public static readonly Histogram<double> QueryDuration = Meter.CreateHistogram<double>(
        "weather.query.duration",
        "ms",
        "Duration of weather queries.");

    /// <summary>Duration of outbound provider calls.</summary>
    public static readonly Histogram<double> ProviderDuration = Meter.CreateHistogram<double>(
        "weather.provider.duration",
        "ms",
        "Duration of outbound weather provider calls.");

    /// <summary>Number of failed queries, tagged by request name.</summary>
    public static readonly Counter<long> QueryFailures = Meter.CreateCounter<long>(
        "weather.query.failures",
        description: "Number of failed weather queries.");

    /// <summary>Number of failed provider calls, tagged by failure kind.</summary>
    public static readonly Counter<long> ProviderFailures = Meter.CreateCounter<long>(
        "weather.provider.failures",
        description: "Number of failed weather provider calls.");

    /// <summary>Number of cache reads served from cache.</summary>
    public static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
        "weather.cache.hits",
        description: "Number of weather cache hits.");

    /// <summary>Number of cache reads that reached the provider.</summary>
    public static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(
        "weather.cache.misses",
        description: "Number of weather cache misses.");

    /// <summary>Number of times a stale snapshot was served because the provider was unavailable.</summary>
    public static readonly Counter<long> StaleServed = Meter.CreateCounter<long>(
        "weather.cache.stale_served",
        description: "Number of responses served from the stale fallback.");

    /// <summary>Number of background refresh executions, tagged by outcome.</summary>
    public static readonly Counter<long> RefreshExecutions = Meter.CreateCounter<long>(
        "weather.refresh.executions",
        description: "Number of background weather refresh executions.");
}
