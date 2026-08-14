using Weather.Domain;

namespace Weather.Application.Tests;

/// <summary>
/// Builders for Domain records. The records carry a lot of fields because the provider does; tests should
/// state only the values they actually assert on, and let everything else default.
/// </summary>
internal static class Fake
{
    public static readonly WeatherCondition Clear = new("Ясно", null, 1000);

    public static HourlyForecast Hour(
        DateTimeOffset localTime,
        double temp = 10,
        double? feelsLike = null,
        int chanceOfRain = 0,
        int chanceOfSnow = 0,
        double precipMm = 0,
        double windKph = 1,
        double uvIndex = 0,
        bool isDay = true,
        WeatherCondition? condition = null)
    {
        return new HourlyForecast(
            localTime,
            new Temperature(temp),
            new Temperature(feelsLike ?? temp),
            condition ?? Clear,
            chanceOfRain,
            chanceOfSnow,
            precipMm,
            windKph,
            WindDegree: 180,
            uvIndex,
            isDay);
    }

    public static DailyForecast Daily(
        DateOnly date,
        double min = 10,
        double max = 20,
        int chanceOfRain = 0,
        int chanceOfSnow = 0,
        double totalPrecipMm = 0,
        double maxWindKph = 5,
        double uvIndex = 0,
        AstroInfo? astro = null,
        WeatherCondition? condition = null)
    {
        return new DailyForecast(
            date,
            new Temperature(min),
            new Temperature(max),
            condition ?? Clear,
            chanceOfRain,
            chanceOfSnow,
            totalPrecipMm,
            maxWindKph,
            uvIndex,
            astro ?? AstroInfo.Unknown);
    }

    public static CurrentWeather Current(
        double temp = 20,
        double? feelsLike = null,
        int humidity = 40,
        double windKph = 8,
        double gustKph = 10,
        double uvIndex = 3,
        WeatherCondition? condition = null,
        DateTimeOffset? observedAt = null)
    {
        return new CurrentWeather(
            new Temperature(temp),
            new Temperature(feelsLike ?? temp),
            humidity,
            windKph,
            WindDegree: 180,
            gustKph,
            PressureMb: 1012,
            uvIndex,
            VisibilityKm: 10,
            PrecipMm: 0,
            IsDay: true,
            condition ?? Clear,
            observedAt ?? new DateTimeOffset(2026, 8, 14, 10, 0, 0, TimeSpan.FromHours(3)));
    }

    public static DayForecast Day(DateOnly date, IReadOnlyList<HourlyForecast> hours, DailyForecast? daily = null)
    {
        return new DayForecast(date, hours, daily ?? Daily(date));
    }

    /// <summary>A full day of hours at the Moscow offset, one per hour in the given inclusive range.</summary>
    public static IReadOnlyList<HourlyForecast> Hours(DateOnly date, int from = 0, int to = 23, TimeSpan? offset = null)
    {
        TimeSpan localOffset = offset ?? TimeSpan.FromHours(3);

        return Enumerable
            .Range(from, to - from + 1)
            .Select(hour => Hour(new DateTimeOffset(date.Year, date.Month, date.Day, hour, 0, 0, localOffset), hour))
            .ToArray();
    }
}
