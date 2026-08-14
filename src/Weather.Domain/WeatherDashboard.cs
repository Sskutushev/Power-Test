namespace Weather.Domain;

public sealed record WeatherDashboard(
    Location Location,
    CurrentWeather Current,
    IReadOnlyList<HourlyForecast> Hourly,
    IReadOnlyList<DailyForecast> Daily,
    DateTimeOffset UpdatedAt,
    DateTimeOffset LocalNow,
    bool IsStale,
    DateTimeOffset? StaleSince);
