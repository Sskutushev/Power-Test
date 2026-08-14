using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Weather.Application.Abstractions;
using Weather.Application.Common;
using Weather.Domain;
using Weather.Infrastructure.WeatherApi.Options;

namespace Weather.Infrastructure.WeatherApi;

internal sealed class CachingWeatherProvider(
    WeatherApiProvider inner,
    HybridCache cache,
    IOptions<WeatherCacheOptions> options,
    TimeProvider timeProvider) : IWeatherProvider
{
    private const string CacheKey = "weather:moscow:v1";
    private const string StaleKey = "weather:moscow:v1:stale";

    public async Task<WeatherSnapshot> GetAsync(Location location, int forecastDays, bool bypassCache, CancellationToken cancellationToken)
    {
        if (bypassCache)
        {
            return await RefreshAsync(location, forecastDays, cancellationToken);
        }

        HybridCacheEntryOptions entryOptions = new()
        {
            Expiration = options.Value.Expiration,
            LocalCacheExpiration = options.Value.LocalCacheExpiration
        };

        try
        {
            return await cache.GetOrCreateAsync(
                CacheKey,
                async token => await RefreshAsync(location, forecastDays, token),
                entryOptions,
                tags: ["weather"],
                cancellationToken);
        }
        catch (WeatherProviderException)
        {
            WeatherSnapshot? stale = await cache.GetOrCreateAsync<WeatherSnapshot?>(
                StaleKey,
                _ => ValueTask.FromResult<WeatherSnapshot?>(null),
                cancellationToken: CancellationToken.None);

            if (stale is not null)
            {
                return stale with { IsStale = true };
            }

            throw;
        }
    }

    private async ValueTask<WeatherSnapshot> RefreshAsync(Location location, int forecastDays, CancellationToken cancellationToken)
    {
        WeatherSnapshot snapshot = await inner.GetAsync(location, forecastDays, bypassCache: true, cancellationToken);
        WeatherSnapshot staleSnapshot = snapshot with { IsStale = true, StaleSince = timeProvider.GetUtcNow() };
        HybridCacheEntryOptions staleOptions = new()
        {
            Expiration = options.Value.StaleExpiration,
            LocalCacheExpiration = options.Value.StaleExpiration
        };
        await cache.SetAsync(StaleKey, staleSnapshot, staleOptions, tags: ["weather"], cancellationToken);

        return snapshot;
    }
}
