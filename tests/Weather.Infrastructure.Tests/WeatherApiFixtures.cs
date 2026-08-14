using System.Globalization;

namespace Weather.Infrastructure.Tests;

/// <summary>
/// WeatherAPI response fixtures shaped after real payloads. No credential appears anywhere in them.
/// </summary>
internal static class WeatherApiFixtures
{
    public const string CurrentSuccess = """
        {
          "location": {
            "name": "Moscow", "region": "Moscow City", "country": "Russia",
            "lat": 55.7522, "lon": 37.6156, "tz_id": "Europe/Moscow",
            "localtime_epoch": 1786710600, "localtime": "2026-08-14 15:30"
          },
          "current": {
            "last_updated_epoch": 1786709700,
            "last_updated": "2026-08-14 15:15",
            "temp_c": 22.3, "temp_f": 72.1, "is_day": 1,
            "condition": { "text": "Переменная облачность", "icon": "//cdn.weatherapi.com/weather/64x64/day/116.png", "code": 1003 },
            "wind_kph": 9.4, "humidity": 44, "feelslike_c": 23.1, "pressure_mb": 1012, "uv": 4.0
          }
        }
        """;

    /// <summary>A day with no <c>day</c> block, blank condition, and a short hour array.</summary>
    public const string ForecastPartial = """
        {
          "location": {
            "name": "Moscow", "lat": 55.7522, "lon": 37.6156, "tz_id": "Europe/Moscow",
            "localtime_epoch": 1786710600, "localtime": "2026-08-14 15:30"
          },
          "current": {
            "last_updated": "2026-08-14 15:15", "temp_c": 22.3, "feelslike_c": 23.1,
            "humidity": 44, "wind_kph": 9.4, "pressure_mb": 1012, "uv": 4.0,
            "condition": { "text": "", "icon": "", "code": 1003 }
          },
          "forecast": {
            "forecastday": [
              {
                "date": "2026-08-14",
                "hour": [
                  { "time": "2026-08-14 20:00", "temp_c": 19.0 },
                  { "time": "2026-08-14 21:00", "temp_c": 18.0, "condition": { "text": "Ясно", "icon": null, "code": 1000 } }
                ]
              }
            ]
          }
        }
        """;

    public const string Malformed = """{ "location": { "name": "Moscow" ,""";

    public const string ProviderError = """{ "error": { "code": 2006, "message": "API key is invalid." } }""";

    public static string ForecastSuccess { get; } = BuildForecast("2026-08-14", "2026-08-15", "2026-08-16");

    /// <summary>Live-shaped forecast: decimal values in fields that look integral.</summary>
    public const string ForecastLiveShape = """
        {
          "location": {
            "name": "Moscow", "lat": 55.7522, "lon": 37.6156, "tz_id": "Europe/Moscow",
            "localtime_epoch": 1786710600, "localtime": "2026-08-14 15:30"
          },
          "current": {
            "last_updated": "2026-08-14 15:15",
            "temp_c": 17.8, "feelslike_c": 17.9,
            "humidity": 72.0, "wind_kph": 11.2, "pressure_mb": 1013.0, "uv": 1.4,
            "condition": { "text": "Пасмурно", "icon": "//cdn.weatherapi.com/weather/64x64/day/122.png", "code": 1009 }
          },
          "forecast": {
            "forecastday": [
              {
                "date": "2026-08-14",
                "day": {
                  "maxtemp_c": 19.4, "mintemp_c": 13.2, "daily_chance_of_rain": 85.0,
                  "condition": { "text": "Дождь", "icon": "//cdn.weatherapi.com/weather/64x64/day/308.png", "code": 1195 }
                },
                "hour": [
                  {
                    "time": "2026-08-14 15:00", "temp_c": 17.8, "wind_kph": 11.2, "chance_of_rain": 64.0,
                    "condition": { "text": "Дождь", "icon": "//cdn.weatherapi.com/weather/64x64/day/308.png", "code": 1195 }
                  }
                ]
              }
            ]
          }
        }
        """;

    public static string BuildForecast(params string[] dates)
    {
        string days = string.Join(',', dates.Select(BuildDay));

        return $$"""
            {
              "location": {
                "name": "Moscow", "region": "Moscow City", "country": "Russia",
                "lat": 55.7522, "lon": 37.6156, "tz_id": "Europe/Moscow",
                "localtime_epoch": 1786710600, "localtime": "2026-08-14 15:30"
              },
              "current": {
                "last_updated_epoch": 1786709700,
                "last_updated": "2026-08-14 15:15",
                "temp_c": 20.1, "feelslike_c": 20.9, "humidity": 40, "wind_kph": 7.2,
                "pressure_mb": 1010, "uv": 3.0,
                "condition": { "text": "Ясно", "icon": "//cdn.weatherapi.com/weather/64x64/day/113.png", "code": 1000 }
              },
              "forecast": { "forecastday": [{{days}}] }
            }
            """;
    }

    private static string BuildDay(string date)
    {
        string hours = string.Join(
            ',',
            Enumerable.Range(0, 24).Select(hour => string.Create(
                CultureInfo.InvariantCulture,
                $$"""
                  {
                    "time": "{{date}} {{hour:00}}:00",
                    "temp_c": {{10 + hour}},
                    "wind_kph": 5.0,
                    "chance_of_rain": 10,
                    "condition": { "text": "Ясно", "icon": "//cdn.weatherapi.com/weather/64x64/day/113.png", "code": 1000 }
                  }
                  """)));

        return $$"""
            {
              "date": "{{date}}",
              "day": {
                "maxtemp_c": 24.0, "mintemp_c": 12.0, "daily_chance_of_rain": 20,
                "condition": { "text": "Ясно", "icon": "//cdn.weatherapi.com/weather/64x64/day/113.png", "code": 1000 }
              },
              "hour": [{{hours}}]
            }
            """;
    }
}
