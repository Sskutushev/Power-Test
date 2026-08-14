using System.ComponentModel.DataAnnotations;

namespace Weather.Application.Weather;

public sealed class WeatherOptions
{
    [Required]
    public string Location { get; init; } = "Moscow";

    [Range(1, 14)]
    public int ForecastDays { get; init; } = 3;

    public string TimeZoneId { get; init; } = "Europe/Moscow";

    public PerformanceOptions Performance { get; init; } = new();

    public BackgroundRefreshOptions BackgroundRefresh { get; init; } = new();
}

public sealed class PerformanceOptions
{
    public TimeSpan SlowQueryThreshold { get; init; } = TimeSpan.FromMilliseconds(500);
}

public sealed class BackgroundRefreshOptions
{
    public bool Enabled { get; init; }

    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(5);
}
