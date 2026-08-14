using System.Text.Json.Serialization;

namespace Weather.Infrastructure.WeatherApi.Contracts;

[JsonSerializable(typeof(WeatherApiForecastResponse))]
[JsonSerializable(typeof(WeatherApiCurrentResponse))]
internal sealed partial class WeatherApiJsonContext : JsonSerializerContext;
