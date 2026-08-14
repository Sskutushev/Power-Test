using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Application.Abstractions;
using Weather.Application.Common;
using Weather.Domain;
using Weather.Infrastructure.WeatherApi.Options;

namespace Weather.Infrastructure.WeatherApi;

/// <summary>
/// Caching decorator over <see cref="WeatherApiProvider"/>.
/// <para>
/// HybridCache supplies the stampede protection: concurrent misses on the same key join one provider
/// call instead of fanning out. A second, longer-lived copy of the last good snapshot backs the stale
/// fallback so a provider outage degrades to old data rather than to an error screen.
/// </para>
/// </summary>
internal sealed class CachingWeatherProvider(
    WeatherApiProvider inner,
    HybridCache cache,
    IOptions<WeatherCacheOptions> options,
    ILogger<CachingWeatherProvider> logger,
    TimeProvider timeProvider) : IWeatherProvider
{
    // The key is versioned and scoped to the coordinates: a visitor sharing their position must never be
    // served the snapshot cached for another point.
    private const string KeyPrefix = "weather:dashboard:v3";

    /// <inheritdoc />
    public async Task<WeatherSnapshot> GetAsync(Location location, int forecastDays, bool bypassCache, CancellationToken cancellationToken)
    {
        if (bypassCache)
        {
            return await RefreshAsync(location, forecastDays, cancellationToken);
        }

        string cacheKey = KeyFor(location);
        WeatherCacheOptions cacheOptions = options.Value;
        HybridCacheEntryOptions entryOptions = new()
        {
            Expiration = cacheOptions.Expiration,
            LocalCacheExpiration = cacheOptions.LocalCacheExpiration
        };

        try
        {
            return await cache.GetOrCreateAsync(
                cacheKey,
                async token =>
                {
                    WeatherTelemetry.CacheMisses.Add(1, new KeyValuePair<string, object?>("cache", "dashboard"));
                    return await RefreshAsync(location, forecastDays, token);
                },
                entryOptions,
                tags: ["weather"],
                cancellationToken);
        }
        catch (WeatherProviderException exception)
        {
            WeatherSnapshot? stale = await cache.TryGetAsync<WeatherSnapshot>(StaleKeyFor(location), CancellationToken.None);

            if (stale is null)
            {
                throw;
            }

            WeatherTelemetry.StaleServed.Add(1, new KeyValuePair<string, object?>("cache", "dashboard"));
            logger.LogWarning(exception, "weather_stale_served {StaleSince}", stale.StaleSince);

            return stale with { IsStale = true };
        }
    }

    private async ValueTask<WeatherSnapshot> RefreshAsync(Location location, int forecastDays, CancellationToken cancellationToken)
    {
        WeatherSnapshot snapshot = await inner.GetAsync(location, forecastDays, bypassCache: true, cancellationToken);
        WeatherCacheOptions cacheOptions = options.Value;
        HybridCacheEntryOptions staleOptions = new()
        {
            Expiration = cacheOptions.StaleExpiration,
            LocalCacheExpiration = cacheOptions.StaleExpiration
        };

        await cache.SetAsync(
            StaleKeyFor(location),
            snapshot with { IsStale = true, StaleSince = timeProvider.GetUtcNow() },
            staleOptions,
            tags: ["weather"],
            cancellationToken);

        return snapshot;
    }

    private static string KeyFor(Location location)
    {
        return $"{KeyPrefix}:{location.Coordinates.ToQueryValue()}";
    }

    private static string StaleKeyFor(Location location)
    {
        return $"{KeyFor(location)}:stale";
    }
}
