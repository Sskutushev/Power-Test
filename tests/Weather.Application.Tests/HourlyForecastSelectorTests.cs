using FluentAssertions;
using Weather.Application.Weather.GetWeatherDashboard;
using Weather.Domain;
using Xunit;

namespace Weather.Application.Tests;

public sealed class HourlyForecastSelectorTests
{
    [Theory]
    [InlineData(0, 48, 0)]
    [InlineData(10, 38, 10)]
    [InlineData(23, 25, 23)]
    public void Select_includes_current_hour_and_all_of_tomorrow(int hour, int expectedCount, int firstHour)
    {
        DateTimeOffset localNow = new(2026, 8, 14, hour, 30, 0, TimeSpan.FromHours(3));
        IReadOnlyList<DayForecast> days = BuildDays();

        IReadOnlyList<HourlyForecast> result = HourlyForecastSelector.Select(days, localNow);

        result.Should().HaveCount(expectedCount);
        result[0].LocalTime.Hour.Should().Be(firstHour);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(18)]
    [InlineData(22)]
    [InlineData(23)]
    public void Select_preserves_count_invariant_for_complete_provider_days(int hour)
    {
        DateTimeOffset localNow = new(2026, 8, 14, hour, 5, 0, TimeSpan.FromHours(3));

        IReadOnlyList<HourlyForecast> result = HourlyForecastSelector.Select(BuildDays(), localNow);

        result.Should().HaveCount(24 - hour + 24);
    }

    [Fact]
    public void Select_returns_available_hours_when_provider_day_is_partial()
    {
        DateTimeOffset localNow = new(2026, 8, 14, 10, 30, 0, TimeSpan.FromHours(3));
        DayForecast partialToday = BuildDay(new DateOnly(2026, 8, 14), 12, 18);
        DayForecast tomorrow = BuildDay(new DateOnly(2026, 8, 15), 0, 23);

        IReadOnlyList<HourlyForecast> result = HourlyForecastSelector.Select([partialToday, tomorrow], localNow);

        result.Should().HaveCount(7 + 24);
        result[0].LocalTime.Hour.Should().Be(12);
    }

    [Fact]
    public void Select_returns_today_remainder_when_only_one_day_is_available()
    {
        DateTimeOffset localNow = new(2026, 8, 14, 20, 0, 0, TimeSpan.FromHours(3));

        IReadOnlyList<HourlyForecast> result = HourlyForecastSelector.Select([BuildDay(new DateOnly(2026, 8, 14), 0, 23)], localNow);

        result.Should().HaveCount(4);
    }

    [Fact]
    public void Select_returns_empty_list_for_empty_provider_result()
    {
        IReadOnlyList<HourlyForecast> result = HourlyForecastSelector.Select([], new DateTimeOffset(2026, 8, 14, 10, 0, 0, TimeSpan.FromHours(3)));

        result.Should().BeEmpty();
    }

    [Fact]
    public void Select_sorts_unsorted_hours_and_removes_duplicates()
    {
        DateOnly today = new(2026, 8, 14);
        HourlyForecast ten = Fake.Hour(new DateTimeOffset(2026, 8, 14, 10, 0, 0, TimeSpan.FromHours(3)));
        HourlyForecast eleven = ten with { LocalTime = ten.LocalTime.AddHours(1) };
        DayForecast day = Fake.Day(today, [eleven, ten, ten]);

        IReadOnlyList<HourlyForecast> result = HourlyForecastSelector.Select([day], new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.FromHours(3)));

        result.Select(hour => hour.LocalTime.Hour).Should().Equal(10, 11);
    }

    [Fact]
    public void Select_compares_provider_local_wall_clock_not_utc_instant()
    {
        DateTimeOffset localNow = new(2026, 8, 14, 10, 30, 0, TimeSpan.FromHours(3));
        DayForecast day = BuildDay(new DateOnly(2026, 8, 14), 9, 11, TimeSpan.Zero);

        IReadOnlyList<HourlyForecast> result = HourlyForecastSelector.Select([day], localNow);

        result.Select(hour => hour.LocalTime.Hour).Should().Equal(10, 11);
    }

    private static IReadOnlyList<DayForecast> BuildDays()
    {
        return
        [
            BuildDay(new DateOnly(2026, 8, 14), 0, 23),
            BuildDay(new DateOnly(2026, 8, 15), 0, 23),
            BuildDay(new DateOnly(2026, 8, 16), 0, 23)
        ];
    }

    private static DayForecast BuildDay(DateOnly date, int startHour, int endHour, TimeSpan? offset = null)
    {
        return Fake.Day(date, Fake.Hours(date, startHour, endHour, offset));
    }
}
