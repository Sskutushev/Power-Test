using Weather.Domain;

namespace Weather.Application.Abstractions;

/// <summary>
/// Provider-independent result of a territory sweep.
/// </summary>
public sealed record RegionalWeatherSnapshot(
    IReadOnlyList<RegionalWeatherPoint> Points,
    bool IsStale = false,
    DateTimeOffset? StaleSince = null);
