using Microsoft.Extensions.Caching.Hybrid;

namespace Weather.Infrastructure.WeatherApi;

/// <summary>
/// Read-only access to <see cref="HybridCache"/>. The public surface only exposes get-or-create,
/// so a plain read has to be expressed as a get-or-create that is forbidden to invoke the factory
/// or to write anything back — otherwise a miss would poison the entry with a null for its whole TTL.
/// </summary>
internal static class HybridCacheExtensions
{
    private static readonly HybridCacheEntryOptions ReadOnlyOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableUnderlyingData
            | HybridCacheEntryFlags.DisableLocalCacheWrite
            | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    /// <summary>Returns the cached value, or <c>null</c> when the key is absent.</summary>
    public static async ValueTask<T?> TryGetAsync<T>(this HybridCache cache, string key, CancellationToken cancellationToken)
        where T : class
    {
        return await cache.GetOrCreateAsync<T?>(
            key,
            static _ => ValueTask.FromResult<T?>(null),
            ReadOnlyOptions,
            cancellationToken: cancellationToken);
    }
}
