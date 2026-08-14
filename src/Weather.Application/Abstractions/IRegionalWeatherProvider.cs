using Weather.Domain;

namespace Weather.Application.Abstractions;

/// <summary>
/// Reads current conditions for a set of map points. Kept separate from <see cref="IWeatherProvider"/>
/// so the dashboard use case never depends on the map feature.
/// </summary>
public interface IRegionalWeatherProvider
{
    /// <summary>
    /// Returns observations for the requested points. Points the provider cannot serve are omitted
    /// rather than failing the whole request; an empty provider result throws.
    /// </summary>
    Task<RegionalWeatherSnapshot> GetAsync(
        IReadOnlyList<Location> points,
        bool bypassCache,
        CancellationToken cancellationToken);
}
