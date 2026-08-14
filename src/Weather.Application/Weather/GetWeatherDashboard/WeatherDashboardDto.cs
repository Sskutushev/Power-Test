namespace Weather.Application.Weather.GetWeatherDashboard;

public sealed record WeatherDashboardDto(
    LocationDto Location,
    CurrentWeatherDto Current,
    IReadOnlyList<HourlyForecastDto> Hourly,
    IReadOnlyList<DailyForecastDto> Daily,
    DateTimeOffset UpdatedAt,
    DateTimeOffset LocalNow,
    bool IsStale,
    DateTimeOffset? StaleSince);

public sealed record LocationDto(string City, string TimeZoneId);

public sealed record WeatherConditionDto(string Text, string? IconUrl, int Code);

public sealed record CurrentWeatherDto(
    double TempC,
    double FeelsLikeC,
    int Humidity,
    double WindKph,
    int PressureMb,
    double UvIndex,
    WeatherConditionDto Condition,
    DateTimeOffset ObservedAt);

public sealed record HourlyForecastDto(
    DateTimeOffset LocalTime,
    double TempC,
    WeatherConditionDto Condition,
    int ChanceOfRain,
    double WindKph);

public sealed record DailyForecastDto(
    DateOnly Date,
    double MinC,
    double MaxC,
    WeatherConditionDto Condition,
    int ChanceOfRain);
