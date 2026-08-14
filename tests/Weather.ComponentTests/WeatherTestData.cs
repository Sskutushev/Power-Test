using Weather.Application.Weather.GetRegionalWeather;
using Weather.Application.Weather.GetWeatherDashboard;

namespace Weather.ComponentTests;

/// <summary>
/// Shared fixtures for component tests. Everything is deterministic: fixed instants, fixed offsets,
/// no dependency on the machine clock or time zone.
/// </summary>
internal static class WeatherTestData
{
    public static readonly DateTimeOffset LocalNow = new(2026, 8, 14, 15, 30, 0, TimeSpan.FromHours(3));

    public static WeatherConditionDto Condition(string text = "Переменная облачность", string? icon = "https://cdn.weatherapi.com/weather/64x64/day/116.png", int code = 1003)
    {
        return new WeatherConditionDto(text, icon, code);
    }

    public static CurrentWeatherDto Current(WeatherConditionDto? condition = null)
    {
        return new CurrentWeatherDto(
            22.3,
            23.1,
            44,
            9.4,
            1012,
            4,
            condition ?? Condition(),
            new DateTimeOffset(2026, 8, 14, 15, 15, 0, TimeSpan.FromHours(3)));
    }

    /// <summary>Three hours of today plus two of tomorrow, so day-boundary behaviour is observable.</summary>
    public static IReadOnlyList<HourlyForecastDto> Hourly()
    {
        WeatherConditionDto condition = Condition();

        return
        [
            new(new DateTimeOffset(2026, 8, 14, 15, 0, 0, TimeSpan.FromHours(3)), 22, condition, 10, 4),
            new(new DateTimeOffset(2026, 8, 14, 16, 0, 0, TimeSpan.FromHours(3)), 21, condition, 15, 4),
            new(new DateTimeOffset(2026, 8, 14, 23, 0, 0, TimeSpan.FromHours(3)), 17, condition, 20, 4),
            new(new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.FromHours(3)), 16, condition, 25, 4),
            new(new DateTimeOffset(2026, 8, 15, 1, 0, 0, TimeSpan.FromHours(3)), 15, condition, 30, 4)
        ];
    }

    public static IReadOnlyList<DailyForecastDto> Daily()
    {
        WeatherConditionDto condition = Condition();

        return
        [
            new(new DateOnly(2026, 8, 14), 12, 24, condition, 20),
            new(new DateOnly(2026, 8, 15), 13, 25, condition, 40),
            new(new DateOnly(2026, 8, 16), 14, 26, condition, 60)
        ];
    }

    public static WeatherDashboardDto Dashboard(bool isStale = false, WeatherConditionDto? condition = null)
    {
        return new WeatherDashboardDto(
            new LocationDto("Москва", "Europe/Moscow"),
            Current(condition),
            Hourly(),
            Daily(),
            new DateTimeOffset(2026, 8, 14, 12, 30, 0, TimeSpan.Zero),
            LocalNow,
            isStale,
            isStale ? new DateTimeOffset(2026, 8, 14, 11, 0, 0, TimeSpan.Zero) : null);
    }

    public static RegionalWeatherDto Region(int points = 3)
    {
        RegionalWeatherPointDto[] mapped = Enumerable
            .Range(0, points)
            .Select(index => new RegionalWeatherPointDto(
                $"Город {index}",
                55.75 + index,
                37.61 + index,
                20 + index,
                19 + index,
                "Ясно",
                "https://cdn.weatherapi.com/weather/64x64/day/113.png",
                5,
                50))
            .ToArray();

        return new RegionalWeatherDto(mapped, 55.7522, 37.6156, 6, LocalNow, false);
    }
}
