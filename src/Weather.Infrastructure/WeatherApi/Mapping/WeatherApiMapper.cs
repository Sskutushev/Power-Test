using System.Globalization;
using Weather.Application.Abstractions;
using Weather.Application.Common;
using Weather.Domain;
using Weather.Infrastructure.WeatherApi.Contracts;

namespace Weather.Infrastructure.WeatherApi.Mapping;

internal static class WeatherApiMapper
{
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    public static WeatherSnapshot Map(WeatherApiForecastResponse forecast, WeatherApiCurrentResponse? currentResponse)
    {
        WeatherApiCurrent current = currentResponse?.Current ?? forecast.Current;
        Location location = new(forecast.Location.Name ?? "Moscow", forecast.Location.TimeZoneId ?? "Europe/Moscow");
        TimeSpan offset = GetOffset(location.TimeZoneId, forecast.Location.LocalTime);
        DateTimeOffset? localNow = ParseLocalDateTime(forecast.Location.LocalTime, offset);

        return new WeatherSnapshot(
            location,
            MapCurrent(current, offset),
            forecast.Forecast.ForecastDays?.Select(day => MapDay(day, offset)).ToArray() ?? [],
            localNow);
    }

    private static CurrentWeather MapCurrent(WeatherApiCurrent current, TimeSpan offset)
    {
        DateTimeOffset observedAt = ParseLocalDateTime(current.LastUpdated, offset) ?? DateTimeOffset.UnixEpoch;
        return new CurrentWeather(
            new Temperature(current.TempC ?? 0),
            new Temperature(current.FeelsLikeC ?? current.TempC ?? 0),
            current.Humidity ?? 0,
            current.WindKph ?? 0,
            current.PressureMb ?? 0,
            current.Uv ?? 0,
            MapCondition(current.Condition),
            observedAt);
    }

    private static DayForecast MapDay(WeatherApiForecastDay day, TimeSpan offset)
    {
        DateOnly date = DateOnly.TryParseExact(day.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed)
            ? parsed
            : DateOnly.MinValue;
        WeatherCondition condition = MapCondition(day.Day?.Condition);
        DailyForecast daily = new(date, new Temperature(day.Day?.MinTempC ?? 0), new Temperature(day.Day?.MaxTempC ?? 0), condition, day.Day?.ChanceOfRain ?? 0);
        HourlyForecast[] hours = day.Hours?.Select(hour => MapHour(hour, offset)).ToArray() ?? [];

        return new DayForecast(date, hours, daily);
    }

    private static HourlyForecast MapHour(WeatherApiHour hour, TimeSpan offset)
    {
        return new HourlyForecast(
            ParseLocalDateTime(hour.Time, offset) ?? DateTimeOffset.UnixEpoch,
            new Temperature(hour.TempC ?? 0),
            MapCondition(hour.Condition),
            hour.ChanceOfRain ?? 0,
            hour.WindKph ?? 0);
    }

    private static WeatherCondition MapCondition(WeatherApiCondition? condition)
    {
        string? icon = condition?.Icon;
        string? normalizedIcon = string.IsNullOrWhiteSpace(icon)
            ? null
            : icon.StartsWith("//", StringComparison.Ordinal) ? $"https:{icon}" : icon;

        return new WeatherCondition(condition?.Text ?? "Нет данных", normalizedIcon, condition?.Code ?? 0);
    }

    private static DateTimeOffset? ParseLocalDateTime(string? value, TimeSpan offset)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTime.TryParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        {
            throw new WeatherProviderProtocolException("WeatherAPI returned an invalid local time.", new FormatException("Invalid WeatherAPI local time."));
        }

        return new DateTimeOffset(parsed, offset);
    }

    private static TimeSpan GetOffset(string timeZoneId, string? localTime)
    {
        DateTime reference = DateTime.TryParseExact(localTime, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
            ? parsed
            : new DateTime(2026, 1, 1, 0, 0, 0);

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return timeZone.GetUtcOffset(reference);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeSpan.FromHours(3);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeSpan.FromHours(3);
        }
    }
}
