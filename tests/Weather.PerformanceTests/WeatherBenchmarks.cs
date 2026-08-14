using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Weather.Application.Weather.GetWeatherDashboard;
using Weather.Domain;
using Weather.Infrastructure.WeatherApi.Contracts;
using Weather.Infrastructure.WeatherApi.Mapping;

namespace Weather.PerformanceTests;

/// <summary>
/// Microbenchmarks for the two pieces of pure computation on the request path: deserialising and mapping
/// a provider payload, and selecting the hourly window. Network calls are deliberately absent — measuring
/// them here would report the stub's latency, not this code's cost.
/// </summary>
[MemoryDiagnoser]
public class WeatherBenchmarks
{
    private IReadOnlyList<DayForecast> days = [];
    private DateTimeOffset localNow;
    private string forecastJson = string.Empty;
    private WeatherApiForecastResponse forecast = null!;

    [GlobalSetup]
    public void Setup()
    {
        forecastJson = BenchmarkPayloads.Forecast;
        forecast = JsonSerializer.Deserialize(forecastJson, WeatherApiJsonContext.Default.WeatherApiForecastResponse)!;
        days = WeatherApiMapper.Map(forecast, null).Days;
        localNow = new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.FromHours(3));
    }

    /// <summary>Full inbound path: JSON to the Application model.</summary>
    [Benchmark]
    public int DeserializeAndMapForecast()
    {
        WeatherApiForecastResponse response = JsonSerializer.Deserialize(forecastJson, WeatherApiJsonContext.Default.WeatherApiForecastResponse)!;

        return WeatherApiMapper.Map(response, null).Days.Count;
    }

    /// <summary>Mapping alone, isolating the allocation cost from the parser's.</summary>
    [Benchmark]
    public int MapForecastOnly()
    {
        return WeatherApiMapper.Map(forecast, null).Days.Count;
    }

    /// <summary>The hourly window selection that the whole task hinges on.</summary>
    [Benchmark]
    public int SelectHourlyWindow()
    {
        return HourlyForecastSelector.Select(days, localNow).Count;
    }
}
