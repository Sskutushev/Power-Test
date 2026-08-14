using System.ComponentModel.DataAnnotations;

namespace Weather.Application.Weather;

/// <summary>
/// Application-level weather settings. The location is fixed by configuration and is not user editable.
/// </summary>
public sealed class WeatherOptions
{
    /// <summary>Display name of the fixed location.</summary>
    [Required]
    public string Location { get; init; } = "Москва";

    /// <summary>Latitude sent to the provider as part of the <c>q=LAT,LON</c> query.</summary>
    [Range(-90d, 90d)]
    public double Latitude { get; init; } = 55.7522;

    /// <summary>Longitude sent to the provider as part of the <c>q=LAT,LON</c> query.</summary>
    [Range(-180d, 180d)]
    public double Longitude { get; init; } = 37.6156;

    /// <summary>Number of forecast days. WeatherAPI counts today as day one.</summary>
    [Range(1, 14)]
    public int ForecastDays { get; init; } = 3;

    /// <summary>IANA time zone used only as a fallback when the provider omits its local time.</summary>
    public string TimeZoneId { get; init; } = "Europe/Moscow";

    /// <summary>Slow-query reporting thresholds.</summary>
    public PerformanceOptions Performance { get; init; } = new();

    /// <summary>Optional scheduled cache refresh.</summary>
    public BackgroundRefreshOptions BackgroundRefresh { get; init; } = new();

    /// <summary>Territory map settings.</summary>
    public RegionOptions Region { get; init; } = new();

    /// <summary>Client-side refresh cadence for an open page.</summary>
    public AutoRefreshOptions AutoRefresh { get; init; } = new();
}

/// <summary>
/// How often an open page re-reads the dashboard. This is a cache read, not a provider call: with a
/// one-minute cadence and a ten-minute cache lifetime the provider still sees at most six calls an hour.
/// </summary>
public sealed class AutoRefreshOptions
{
    /// <summary>Turns the periodic refresh off entirely.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Interval between refreshes while the page is open.</summary>
    [Range(typeof(TimeSpan), "00:00:15", "01:00:00")]
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(1);
}

/// <summary>Thresholds for the MediatR performance behavior.</summary>
public sealed class PerformanceOptions
{
    /// <summary>Queries slower than this threshold are logged as warnings.</summary>
    public TimeSpan SlowQueryThreshold { get; init; } = TimeSpan.FromMilliseconds(500);
}

/// <summary>Settings for the optional background cache refresh service.</summary>
public sealed class BackgroundRefreshOptions
{
    /// <summary>Registers the hosted service when enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Refresh period.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(5);
}

/// <summary>Settings for the territory forecast map.</summary>
public sealed class RegionOptions
{
    /// <summary>Disables the map feature and its provider calls entirely.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Initial map zoom level.</summary>
    [Range(1, 18)]
    public int Zoom { get; init; } = 6;

    /// <summary>Points sampled across the territory. Each point costs one provider call per cache period.</summary>
    public IReadOnlyList<RegionPointOptions> Points { get; init; } = [];
}

/// <summary>A single sampled point of the territory map.</summary>
public sealed class RegionPointOptions
{
    /// <summary>Display name shown on the map marker.</summary>
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>Point latitude.</summary>
    [Range(-90d, 90d)]
    public double Latitude { get; init; }

    /// <summary>Point longitude.</summary>
    [Range(-180d, 180d)]
    public double Longitude { get; init; }
}
