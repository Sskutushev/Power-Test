using System.Globalization;

namespace Weather.PerformanceTests;

/// <summary>A realistic three-day WeatherAPI payload used as benchmark input.</summary>
internal static class BenchmarkPayloads
{
    public static string Forecast { get; } = Build("2026-08-14", "2026-08-15", "2026-08-16");

    private static string Build(params string[] dates)
    {
        string days = string.Join(',', dates.Select(BuildDay));

        return $$"""
            {
              "location": { "name": "Moscow", "lat": 55.7522, "lon": 37.6156, "tz_id": "Europe/Moscow", "localtime": "2026-08-14 10:30" },
              "current": {
                "last_updated": "2026-08-14 10:15",
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
