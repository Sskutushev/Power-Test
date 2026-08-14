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
            TempC: 22.3,
            FeelsLikeC: 23.1,
            Humidity: 44,
            WindKph: 9.4,
            WindDegree: 225,
            GustKph: 14.2,
            PressureMb: 1012,
            UvIndex: 4,
            VisibilityKm: 10,
            PrecipMm: 0,
            IsDay: true,
            condition ?? Condition(),
            new DateTimeOffset(2026, 8, 14, 15, 15, 0, TimeSpan.FromHours(3)));
    }

    public static HourlyForecastDto Hour(DateTimeOffset localTime, double temp, int chanceOfRain = 10, double precipMm = 0, bool isDay = true)
    {
        return new HourlyForecastDto(
            localTime,
            temp,
            temp - 1,
            Condition(),
            chanceOfRain,
            ChanceOfSnow: 0,
            precipMm,
            WindKph: 4,
            WindDegree: 200,
            UvIndex: 3,
            isDay);
    }

    /// <summary>Three hours of today plus two of tomorrow, so day-boundary behaviour is observable.</summary>
    public static IReadOnlyList<HourlyForecastDto> Hourly()
    {
        return
        [
            Hour(new DateTimeOffset(2026, 8, 14, 15, 0, 0, TimeSpan.FromHours(3)), 22, 10),
            Hour(new DateTimeOffset(2026, 8, 14, 16, 0, 0, TimeSpan.FromHours(3)), 21, 15),
            Hour(new DateTimeOffset(2026, 8, 14, 23, 0, 0, TimeSpan.FromHours(3)), 17, 20, isDay: false),
            Hour(new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.FromHours(3)), 16, 25, isDay: false),
            Hour(new DateTimeOffset(2026, 8, 15, 1, 0, 0, TimeSpan.FromHours(3)), 15, 30, isDay: false)
        ];
    }

    public static IReadOnlyList<DailyForecastDto> Daily()
    {
        return
        [
            Day(new DateOnly(2026, 8, 14), 12, 24, 20),
            Day(new DateOnly(2026, 8, 15), 13, 25, 40),
            Day(new DateOnly(2026, 8, 16), 14, 26, 60)
        ];
    }

    public static DailyForecastDto Day(DateOnly date, double min, double max, int chanceOfRain)
    {
        HourlyForecastDto[] hours = Enumerable
            .Range(0, 24)
            .Select(hour => Hour(new DateTimeOffset(date.Year, date.Month, date.Day, hour, 0, 0, TimeSpan.FromHours(3)), min + hour, chanceOfRain, isDay: hour is > 5 and < 21))
            .ToArray();

        return new DailyForecastDto(
            date,
            min,
            max,
            Condition(),
            chanceOfRain,
            ChanceOfSnow: 0,
            TotalPrecipMm: 1.2,
            MaxWindKph: 18,
            UvIndex: 4,
            new AstroDto(new TimeOnly(5, 12), new TimeOnly(20, 44), "Waxing Crescent", new TimeSpan(15, 32, 0)),
            hours);
    }

    public static WeatherDashboardDto Dashboard(bool isStale = false, WeatherConditionDto? condition = null)
    {
        return new WeatherDashboardDto(
            new LocationDto("Москва", "Europe/Moscow"),
            Current(condition),
            Hourly(),
            Daily(),
            Advisories(),
            new DateTimeOffset(2026, 8, 14, 12, 30, 0, TimeSpan.Zero),
            LocalNow,
            isStale,
            isStale ? new DateTimeOffset(2026, 8, 14, 11, 0, 0, TimeSpan.Zero) : null);
    }

    public static IReadOnlyList<WeatherAdvisoryDto> Advisories()
    {
        return
        [
            new(WeatherAdvisoryKind.Rain, "Дождь около 18:00 — зонт пригодится. Вероятность 60 %."),
            new(WeatherAdvisoryKind.Clothing, "Комфортно: лёгкая одежда, к вечеру пригодится кофта.")
        ];
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
