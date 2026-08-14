using System.Text.Json;
using FluentAssertions;
using Weather.Infrastructure.WeatherApi.Contracts;
using Weather.Infrastructure.WeatherApi.Mapping;
using Xunit;

namespace Weather.Infrastructure.Tests;

public sealed class WeatherApiMapperTests
{
    [Fact]
    public void Forecast_json_maps_to_provider_independent_snapshot()
    {
        WeatherApiForecastResponse? forecast = JsonSerializer.Deserialize(SampleForecastJson, WeatherApiJsonContext.Default.WeatherApiForecastResponse);
        WeatherApiCurrentResponse? current = JsonSerializer.Deserialize(SampleCurrentJson, WeatherApiJsonContext.Default.WeatherApiCurrentResponse);

        Weather.Application.Abstractions.WeatherSnapshot snapshot = WeatherApiMapper.Map(forecast!, current);

        snapshot.Location.City.Should().Be("Moscow");
        snapshot.Location.TimeZoneId.Should().Be("Europe/Moscow");
        snapshot.Current.Temp.Celsius.Should().Be(22.3);
        snapshot.Current.Condition.IconUrl.Should().Be("https://cdn.weatherapi.com/weather/64x64/day/116.png");
        snapshot.Days.Should().HaveCount(3);
        snapshot.Days[0].Hours.Should().HaveCount(24);
        snapshot.LocalNow.Should().Be(new DateTimeOffset(2026, 8, 14, 15, 30, 0, TimeSpan.FromHours(3)));
    }

    private const string SampleCurrentJson = """
        {
          "location": { "name": "Moscow", "tz_id": "Europe/Moscow", "localtime": "2026-08-14 15:30", "localtime_epoch": 1786710600 },
          "current": {
            "last_updated": "2026-08-14 15:15",
            "last_updated_epoch": 1786709700,
            "temp_c": 22.3,
            "feelslike_c": 23.1,
            "humidity": 44,
            "wind_kph": 9.4,
            "pressure_mb": 1012,
            "uv": 4.0,
            "condition": { "text": "Переменная облачность", "icon": "//cdn.weatherapi.com/weather/64x64/day/116.png", "code": 1003 }
          }
        }
        """;

    private static readonly string SampleForecastJson = $$"""
        {
          "location": { "name": "Moscow", "tz_id": "Europe/Moscow", "localtime": "2026-08-14 15:30", "localtime_epoch": 1786710600 },
          "current": {
            "last_updated": "2026-08-14 15:15",
            "last_updated_epoch": 1786709700,
            "temp_c": 22.3,
            "feelslike_c": 23.1,
            "humidity": 44,
            "wind_kph": 9.4,
            "pressure_mb": 1012,
            "uv": 4.0,
            "condition": { "text": "Переменная облачность", "icon": "//cdn.weatherapi.com/weather/64x64/day/116.png", "code": 1003 }
          },
          "forecast": {
            "forecastday": [
              {{BuildDay("2026-08-14")}},
              {{BuildDay("2026-08-15")}},
              {{BuildDay("2026-08-16")}}
            ]
          }
        }
        """;

    private static string BuildDay(string date)
    {
        string hours = string.Join(
            ',',
            Enumerable.Range(0, 24).Select(hour => $$"""
                {
                  "time": "{{date}} {{hour:00}}:00",
                  "temp_c": {{10 + hour}},
                  "wind_kph": 5.0,
                  "chance_of_rain": 10,
                  "condition": { "text": "Ясно", "icon": "", "code": 1000 }
                }
                """));

        return $$"""
            {
              "date": "{{date}}",
              "day": {
                "maxtemp_c": 24.0,
                "mintemp_c": 12.0,
                "daily_chance_of_rain": 20,
                "condition": { "text": "Ясно", "icon": "//cdn.weatherapi.com/weather/64x64/day/113.png", "code": 1000 }
              },
              "hour": [{{hours}}]
            }
            """;
    }
}
