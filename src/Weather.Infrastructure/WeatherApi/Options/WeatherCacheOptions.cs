namespace Weather.Infrastructure.WeatherApi.Options;

/// <summary>
/// Cache lifetimes. Values are bounded on purpose: an unbounded in-memory cache grows until the
/// process is under memory pressure, and a weather snapshot has no value once it is hours old.
/// </summary>
public sealed class WeatherCacheOptions
{
    /// <summary>In-process lifetime of the dashboard snapshot.</summary>
    public TimeSpan LocalCacheExpiration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Distributed lifetime of the dashboard snapshot.</summary>
    public TimeSpan Expiration { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Lifetime of the territory sweep. Longer, because each refresh costs one call per map point.</summary>
    public TimeSpan RegionExpiration { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Lifetime of the last-good copy used when the provider is unavailable.</summary>
    public TimeSpan StaleExpiration { get; init; } = TimeSpan.FromHours(1);
}
