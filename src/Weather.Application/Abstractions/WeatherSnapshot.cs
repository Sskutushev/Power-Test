using Weather.Domain;

namespace Weather.Application.Abstractions;

public sealed record WeatherSnapshot(
    Location Location,
    CurrentWeather Current,
    IReadOnlyList<DayForecast> Days,
    DateTimeOffset? LocalNow,
    bool IsStale = false,
    DateTimeOffset? StaleSince = null);
