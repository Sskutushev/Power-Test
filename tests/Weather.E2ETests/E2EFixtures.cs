using System.Globalization;

namespace Weather.E2ETests;

/// <summary>Deterministic WeatherAPI payloads for browser runs. No credential inside.</summary>
internal static class E2EFixtures
{
    public const string Current = """
        {
          "location": { "name": "Moscow", "lat": 55.7522, "lon": 37.6156, "tz_id": "Europe/Moscow", "localtime": "2026-08-14 15:30" },
          "current": {
            "last_updated": "2026-08-14 15:15",
            "temp_c": 22.3, "feelslike_c": 23.1, "humidity": 44, "wind_kph": 9.4,
            "pressure_mb": 1012, "uv": 4.0,
            "condition": { "text": "Переменная облачность", "icon": "//cdn.weatherapi.com/weather/64x64/day/116.png", "code": 1003 }
          }
        }
        """;

    public static string Forecast { get; } = Build("2026-08-14", "2026-08-15", "2026-08-16");

    private static string Build(params string[] dates)
    {
        string days = string.Join(',', dates.Select(BuildDay));

        return $$"""
            {
              "location": { "name": "Moscow", "lat": 55.7522, "lon": 37.6156, "tz_id": "Europe/Moscow", "localtime": "2026-08-14 15:30" },
              "current": {
                "last_updated": "2026-08-14 15:15",
                "temp_c": 22.3, "feelslike_c": 23.1, "humidity": 44, "wind_kph": 9.4,
                "pressure_mb": 1012, "uv": 4.0,
                "condition": { "text": "Переменная облачность", "icon": "//cdn.weatherapi.com/weather/64x64/day/116.png", "code": 1003 }
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
                    "temp_c": {{10 + hour}}, "wind_kph": 5.0, "chance_of_rain": 10,
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
