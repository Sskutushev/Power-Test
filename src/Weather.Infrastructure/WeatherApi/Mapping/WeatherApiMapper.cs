using System.Globalization;
using Weather.Application.Abstractions;
using Weather.Application.Common;
using Weather.Domain;
using Weather.Infrastructure.WeatherApi.Contracts;

namespace Weather.Infrastructure.WeatherApi.Mapping;

/// <summary>
/// Translates WeatherAPI payloads into the provider-independent model. Every field is treated as
/// optional: the contract allows nulls and blanks, and a partial response must degrade rather than throw.
/// </summary>
internal static class WeatherApiMapper
{
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    /// <summary>Astro times arrive as 12-hour strings; some responses use a 24-hour form instead.</summary>
    private static readonly string[] AstroTimeFormats = ["hh:mm tt", "h:mm tt", "HH:mm", "H:mm"];

    public static WeatherSnapshot Map(WeatherApiForecastResponse forecast, WeatherApiCurrentResponse? currentResponse)
    {
        WeatherApiCurrent current = currentResponse?.Current ?? forecast.Current;
        Location location = new(
            forecast.Location.Name ?? "Moscow",
            forecast.Location.TimeZoneId ?? "Europe/Moscow",
            new GeoPoint(forecast.Location.Latitude ?? 0, forecast.Location.Longitude ?? 0));
        TimeSpan offset = GetOffset(location.TimeZoneId, forecast.Location.LocalTime);
        DateTimeOffset? localNow = ParseLocalDateTime(forecast.Location.LocalTime, offset);

        return new WeatherSnapshot(
            location,
            MapCurrent(current, offset),
            forecast.Forecast.ForecastDays?.Select(day => MapDay(day, offset)).ToArray() ?? [],
            localNow);
    }

    /// <summary>
    /// Maps a single <c>current.json</c> response onto a territory map point. The configured point name and
    /// coordinates win over the provider's own resolution so markers stay where the map expects them.
    /// </summary>
    public static RegionalWeatherPoint MapRegionPoint(Location requested, WeatherApiCurrentResponse response)
    {
        WeatherApiCurrent current = response.Current;

        return new RegionalWeatherPoint(
            requested.City,
            requested.Coordinates,
            new Temperature(current.TempC ?? 0),
            new Temperature(current.FeelsLikeC ?? current.TempC ?? 0),
            MapCondition(current.Condition),
            current.WindKph ?? 0,
            Round(current.Humidity));
    }

    private static CurrentWeather MapCurrent(WeatherApiCurrent current, TimeSpan offset)
    {
        DateTimeOffset observedAt = ParseLocalDateTime(current.LastUpdated, offset) ?? DateTimeOffset.UnixEpoch;

        return new CurrentWeather(
            new Temperature(current.TempC ?? 0),
            new Temperature(current.FeelsLikeC ?? current.TempC ?? 0),
            Round(current.Humidity),
            current.WindKph ?? 0,
            Round(current.WindDegree),
            current.GustKph ?? 0,
            Round(current.PressureMb),
            current.Uv ?? 0,
            current.VisibilityKm ?? 0,
            current.PrecipMm ?? 0,
            // The provider omits is_day on some responses; daylight is the safer default for contrast.
            current.IsDay is not 0,
            MapCondition(current.Condition),
            observedAt);
    }

    private static DayForecast MapDay(WeatherApiForecastDay day, TimeSpan offset)
    {
        DateOnly date = DateOnly.TryParseExact(day.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed)
            ? parsed
            : DateOnly.MinValue;

        DailyForecast daily = new(
            date,
            new Temperature(day.Day?.MinTempC ?? 0),
            new Temperature(day.Day?.MaxTempC ?? 0),
            MapCondition(day.Day?.Condition),
            Round(day.Day?.ChanceOfRain),
            Round(day.Day?.ChanceOfSnow),
            day.Day?.TotalPrecipMm ?? 0,
            day.Day?.MaxWindKph ?? 0,
            day.Day?.Uv ?? 0,
            MapAstro(day.Astro));

        HourlyForecast[] hours = day.Hours?.Select(hour => MapHour(hour, offset)).ToArray() ?? [];

        return new DayForecast(date, hours, daily);
    }

    private static HourlyForecast MapHour(WeatherApiHour hour, TimeSpan offset)
    {
        return new HourlyForecast(
            ParseLocalDateTime(hour.Time, offset) ?? DateTimeOffset.UnixEpoch,
            new Temperature(hour.TempC ?? 0),
            new Temperature(hour.FeelsLikeC ?? hour.TempC ?? 0),
            MapCondition(hour.Condition),
            Round(hour.ChanceOfRain),
            Round(hour.ChanceOfSnow),
            hour.PrecipMm ?? 0,
            hour.WindKph ?? 0,
            Round(hour.WindDegree),
            hour.Uv ?? 0,
            hour.IsDay is not 0);
    }

    private static AstroInfo MapAstro(WeatherApiAstro? astro)
    {
        if (astro is null)
        {
            return AstroInfo.Unknown;
        }

        return new AstroInfo(
            ParseAstroTime(astro.Sunrise),
            ParseAstroTime(astro.Sunset),
            string.IsNullOrWhiteSpace(astro.MoonPhase) ? null : astro.MoonPhase);
    }

    /// <summary>
    /// Astro times are supplementary, so an unparseable value degrades to "unknown" rather than failing the
    /// whole response the way an unparseable forecast timestamp does.
    /// </summary>
    private static TimeOnly? ParseAstroTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TimeOnly.TryParseExact(value.Trim(), AstroTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly parsed)
            ? parsed
            : null;
    }

    private static WeatherCondition MapCondition(WeatherApiCondition? condition)
    {
        string? icon = condition?.Icon;
        string? normalizedIcon = string.IsNullOrWhiteSpace(icon)
            ? null
            : icon.StartsWith("//", StringComparison.Ordinal) ? $"https:{icon}" : icon;

        // A blank string is as absent as a null here: the provider returns both for partial data.
        string text = string.IsNullOrWhiteSpace(condition?.Text) ? "Нет данных" : condition.Text;

        return new WeatherCondition(text, normalizedIcon, condition?.Code ?? 0);
    }

    /// <summary>Provider integers arrive as decimals; rounding here keeps the Domain model integral.</summary>
    private static int Round(double? value)
    {
        return value is null ? 0 : (int)Math.Round(value.Value, MidpointRounding.AwayFromZero);
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
