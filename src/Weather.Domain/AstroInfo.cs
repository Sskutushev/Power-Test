namespace Weather.Domain;

/// <summary>
/// Sun and moon information for a forecast day. It ships inside the same <c>forecast.json</c> response the
/// dashboard already pays for, so surfacing it costs nothing extra.
/// </summary>
public sealed record AstroInfo(
    TimeOnly? Sunrise,
    TimeOnly? Sunset,
    string? MoonPhase)
{
    /// <summary>Empty astro block, used when the provider omits it.</summary>
    public static readonly AstroInfo Unknown = new(null, null, null);

    /// <summary>Length of the day, when both ends are known.</summary>
    public TimeSpan? DayLength => Sunrise is { } rise && Sunset is { } set && set > rise
        ? set - rise
        : null;
}
