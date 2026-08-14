using System.Text.Json.Serialization;

namespace Weather.Infrastructure.WeatherApi.Contracts;

internal sealed record WeatherApiForecastResponse(
    [property: JsonPropertyName("location")] WeatherApiLocation Location,
    [property: JsonPropertyName("current")] WeatherApiCurrent Current,
    [property: JsonPropertyName("forecast")] WeatherApiForecast Forecast);

internal sealed record WeatherApiCurrentResponse(
    [property: JsonPropertyName("location")] WeatherApiLocation Location,
    [property: JsonPropertyName("current")] WeatherApiCurrent Current);

internal sealed record WeatherApiLocation(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("tz_id")] string? TimeZoneId,
    [property: JsonPropertyName("localtime")] string? LocalTime,
    [property: JsonPropertyName("lat")] double? Latitude,
    [property: JsonPropertyName("lon")] double? Longitude);

internal sealed record WeatherApiCurrent(
    [property: JsonPropertyName("last_updated")] string? LastUpdated,
    [property: JsonPropertyName("temp_c")] double? TempC,
    [property: JsonPropertyName("feelslike_c")] double? FeelsLikeC,
    // WeatherAPI sends pressure as a decimal (1013.0) even though it reads like an integer, and the same
    // is true of humidity and rain chance. Binding them as Int32 makes the live API fail to deserialise
    // while every hand-written fixture keeps passing.
    [property: JsonPropertyName("humidity")] double? Humidity,
    [property: JsonPropertyName("wind_kph")] double? WindKph,
    [property: JsonPropertyName("wind_degree")] double? WindDegree,
    [property: JsonPropertyName("gust_kph")] double? GustKph,
    [property: JsonPropertyName("pressure_mb")] double? PressureMb,
    [property: JsonPropertyName("uv")] double? Uv,
    [property: JsonPropertyName("vis_km")] double? VisibilityKm,
    [property: JsonPropertyName("precip_mm")] double? PrecipMm,
    [property: JsonPropertyName("is_day")] int? IsDay,
    [property: JsonPropertyName("condition")] WeatherApiCondition? Condition);

internal sealed record WeatherApiForecast(
    [property: JsonPropertyName("forecastday")] IReadOnlyList<WeatherApiForecastDay>? ForecastDays);

internal sealed record WeatherApiForecastDay(
    [property: JsonPropertyName("date")] string? Date,
    [property: JsonPropertyName("day")] WeatherApiDay? Day,
    [property: JsonPropertyName("astro")] WeatherApiAstro? Astro,
    [property: JsonPropertyName("hour")] IReadOnlyList<WeatherApiHour>? Hours);

internal sealed record WeatherApiDay(
    [property: JsonPropertyName("maxtemp_c")] double? MaxTempC,
    [property: JsonPropertyName("mintemp_c")] double? MinTempC,
    [property: JsonPropertyName("maxwind_kph")] double? MaxWindKph,
    [property: JsonPropertyName("totalprecip_mm")] double? TotalPrecipMm,
    [property: JsonPropertyName("daily_chance_of_rain")] double? ChanceOfRain,
    [property: JsonPropertyName("daily_chance_of_snow")] double? ChanceOfSnow,
    [property: JsonPropertyName("uv")] double? Uv,
    [property: JsonPropertyName("condition")] WeatherApiCondition? Condition);

/// <summary>Sunrise and sunset arrive as localised 12-hour strings such as <c>05:12 AM</c>.</summary>
internal sealed record WeatherApiAstro(
    [property: JsonPropertyName("sunrise")] string? Sunrise,
    [property: JsonPropertyName("sunset")] string? Sunset,
    [property: JsonPropertyName("moon_phase")] string? MoonPhase);

internal sealed record WeatherApiHour(
    [property: JsonPropertyName("time")] string? Time,
    [property: JsonPropertyName("temp_c")] double? TempC,
    [property: JsonPropertyName("feelslike_c")] double? FeelsLikeC,
    [property: JsonPropertyName("wind_kph")] double? WindKph,
    [property: JsonPropertyName("wind_degree")] double? WindDegree,
    [property: JsonPropertyName("chance_of_rain")] double? ChanceOfRain,
    [property: JsonPropertyName("chance_of_snow")] double? ChanceOfSnow,
    [property: JsonPropertyName("precip_mm")] double? PrecipMm,
    [property: JsonPropertyName("uv")] double? Uv,
    [property: JsonPropertyName("is_day")] int? IsDay,
    [property: JsonPropertyName("condition")] WeatherApiCondition? Condition);

internal sealed record WeatherApiCondition(
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("icon")] string? Icon,
    [property: JsonPropertyName("code")] int? Code);
