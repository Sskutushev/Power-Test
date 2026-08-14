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
    [property: JsonPropertyName("localtime_epoch")] long? LocalTimeEpoch);

internal sealed record WeatherApiCurrent(
    [property: JsonPropertyName("last_updated")] string? LastUpdated,
    [property: JsonPropertyName("last_updated_epoch")] long? LastUpdatedEpoch,
    [property: JsonPropertyName("temp_c")] double? TempC,
    [property: JsonPropertyName("feelslike_c")] double? FeelsLikeC,
    [property: JsonPropertyName("humidity")] int? Humidity,
    [property: JsonPropertyName("wind_kph")] double? WindKph,
    [property: JsonPropertyName("pressure_mb")] int? PressureMb,
    [property: JsonPropertyName("uv")] double? Uv,
    [property: JsonPropertyName("condition")] WeatherApiCondition? Condition);

internal sealed record WeatherApiForecast(
    [property: JsonPropertyName("forecastday")] IReadOnlyList<WeatherApiForecastDay>? ForecastDays);

internal sealed record WeatherApiForecastDay(
    [property: JsonPropertyName("date")] string? Date,
    [property: JsonPropertyName("day")] WeatherApiDay? Day,
    [property: JsonPropertyName("hour")] IReadOnlyList<WeatherApiHour>? Hours);

internal sealed record WeatherApiDay(
    [property: JsonPropertyName("maxtemp_c")] double? MaxTempC,
    [property: JsonPropertyName("mintemp_c")] double? MinTempC,
    [property: JsonPropertyName("daily_chance_of_rain")] int? ChanceOfRain,
    [property: JsonPropertyName("condition")] WeatherApiCondition? Condition);

internal sealed record WeatherApiHour(
    [property: JsonPropertyName("time")] string? Time,
    [property: JsonPropertyName("temp_c")] double? TempC,
    [property: JsonPropertyName("wind_kph")] double? WindKph,
    [property: JsonPropertyName("chance_of_rain")] int? ChanceOfRain,
    [property: JsonPropertyName("condition")] WeatherApiCondition? Condition);

internal sealed record WeatherApiCondition(
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("icon")] string? Icon,
    [property: JsonPropertyName("code")] int? Code);
