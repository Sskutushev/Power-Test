using BenchmarkDotNet.Attributes;
using FluentAssertions;
using Weather.Application.Weather.GetWeatherDashboard;
using Weather.Domain;
using Xunit;

namespace Weather.PerformanceTests;

public sealed class HourlySelectorPerformanceSmokeTests
{
    [Fact]
    public void Benchmark_target_produces_expected_result()
    {
        HourlySelectorBenchmarks benchmark = new();
        benchmark.Setup();

        IReadOnlyList<HourlyForecast> result = benchmark.SelectForecast();

        result.Should().HaveCount(38);
    }
}

[MemoryDiagnoser]
public class HourlySelectorBenchmarks
{
    private IReadOnlyList<DayForecast> days = [];
    private DateTimeOffset localNow;

    [GlobalSetup]
    public void Setup()
    {
        WeatherCondition condition = new("Ясно", null, 1000);
        days = Enumerable.Range(0, 3)
            .Select(dayOffset =>
            {
                DateOnly date = new DateOnly(2026, 8, 14).AddDays(dayOffset);
                DailyForecast daily = new(date, new Temperature(10), new Temperature(20), condition, 0);
                HourlyForecast[] hours = Enumerable.Range(0, 24)
                    .Select(hour => new HourlyForecast(new DateTimeOffset(date.Year, date.Month, date.Day, hour, 0, 0, TimeSpan.FromHours(3)), new Temperature(hour), condition, 0, 1))
                    .ToArray();
                return new DayForecast(date, hours, daily);
            })
            .ToArray();
        localNow = new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.FromHours(3));
    }

    [Benchmark]
    public IReadOnlyList<HourlyForecast> SelectForecast()
    {
        return HourlyForecastSelector.Select(days, localNow);
    }
}
