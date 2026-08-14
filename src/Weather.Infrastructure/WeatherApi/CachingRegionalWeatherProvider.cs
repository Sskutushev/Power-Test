using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Application.Abstractions;
using Weather.Application.Common;
using Weather.Domain;
using Weather.Infrastructure.WeatherApi.Options;

namespace Weather.Infrastructure.WeatherApi;

/// <summary>
/// Caching decorator over <see cref="WeatherApiRegionalProvider"/>. The territory sweep costs one
/// provider call per map point, so it is cached harder than the dashboard and always has a stale copy.
/// </summary>
internal sealed class CachingRegionalWeatherProvider(
    WeatherApiRegionalProvider inner,
    HybridCache cache,
    IOptions<WeatherCacheOptions> options,
    ILogger<CachingRegionalWeatherProvider> logger,
    TimeProvider timeProvider) : IRegionalWeatherProvider
{
    private const string CacheKey = "weather:region:v1";
    private const string StaleKey = "weather:region:v1:stale";

    /// <inheritdoc />
    public async Task<RegionalWeatherSnapshot> GetAsync(
        IReadOnlyList<Location> points,
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        if (bypassCache)
        {
            return await RefreshAsync(points, cancellationToken);
        }

        WeatherCacheOptions cacheOptions = options.Value;
        HybridCacheEntryOptions entryOptions = new()
        {
            Expiration = cacheOptions.RegionExpiration,
            LocalCacheExpiration = cacheOptions.RegionExpiration
        };

        try
        {
            return await cache.GetOrCreateAsync(
                CacheKey,
                async token =>
                {
                    WeatherTelemetry.CacheMisses.Add(1, new KeyValuePair<string, object?>("cache", "region"));
                    return await RefreshAsync(points, token);
                },
                entryOptions,
                tags: ["weather"],
                cancellationToken);
        }
        catch (WeatherProviderException exception)
        {
            RegionalWeatherSnapshot? stale = await cache.TryGetAsync<RegionalWeatherSnapshot>(StaleKey, CancellationToken.None);

            if (stale is null)
            {
                throw;
            }

            WeatherTelemetry.StaleServed.Add(1, new KeyValuePair<string, object?>("cache", "region"));
            logger.LogWarning(exception, "weather_region_stale_served {StaleSince}", stale.StaleSince);

            return stale with { IsStale = true };
        }
    }

    private async ValueTask<RegionalWeatherSnapshot> RefreshAsync(IReadOnlyList<Location> points, CancellationToken cancellationToken)
    {
        RegionalWeatherSnapshot snapshot = await inner.GetAsync(points, bypassCache: true, cancellationToken);
        WeatherCacheOptions cacheOptions = options.Value;
        HybridCacheEntryOptions staleOptions = new()
        {
            Expiration = cacheOptions.StaleExpiration,
            LocalCacheExpiration = cacheOptions.StaleExpiration
        };

        await cache.SetAsync(
            StaleKey,
            snapshot with { IsStale = true, StaleSince = timeProvider.GetUtcNow() },
            staleOptions,
            tags: ["weather"],
            cancellationToken);

        return snapshot;
    }
}
