using FluentAssertions;
using Xunit;

namespace Weather.PerformanceTests;

/// <summary>
/// Guards the benchmark targets. A benchmark that silently starts measuring the wrong thing is worse
/// than no benchmark, so each target is asserted to produce the expected result before it is timed.
/// </summary>
public sealed class BenchmarkContractTests
{
    private readonly WeatherBenchmarks benchmarks = new();

    public BenchmarkContractTests()
    {
        benchmarks.Setup();
    }

    [Fact]
    public void Deserialise_and_map_target_produces_three_days()
    {
        benchmarks.DeserializeAndMapForecast().Should().Be(3);
    }

    [Fact]
    public void Map_only_target_produces_three_days()
    {
        benchmarks.MapForecastOnly().Should().Be(3);
    }

    [Fact]
    public void Hourly_window_target_selects_the_remaining_hours_plus_tomorrow()
    {
        // 10:30 local: hours 10..23 today (14) plus all 24 of tomorrow.
        benchmarks.SelectHourlyWindow().Should().Be(38);
    }
}
