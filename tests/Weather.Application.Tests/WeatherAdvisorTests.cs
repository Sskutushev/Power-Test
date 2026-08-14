using FluentAssertions;
using Weather.Application.Weather.GetWeatherDashboard;
using Weather.Domain;
using Xunit;

namespace Weather.Application.Tests;

/// <summary>
/// The advisor is what turns readings into answers, so its rules are asserted one by one. It is a pure
/// function with no clock access, which is what makes this level of coverage cheap.
/// </summary>
public sealed class WeatherAdvisorTests
{
    private static readonly DateTimeOffset LocalNow = new(2026, 8, 14, 12, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Rain_within_the_horizon_is_announced_with_its_hour()
    {
        IReadOnlyList<HourlyForecast> hourly =
        [
            Hour(12, chanceOfRain: 5),
            Hour(13, chanceOfRain: 10),
            Hour(16, chanceOfRain: 80, precipMm: 1.2)
        ];

        IReadOnlyList<WeatherAdvisoryDto> result = Advise(hourly);

        result.Should().Contain(advisory => advisory.Kind == WeatherAdvisoryKind.Rain);
        result.First(advisory => advisory.Kind == WeatherAdvisoryKind.Rain).Text.Should().Contain("16:00");
    }

    [Fact]
    public void Rain_already_falling_is_reported_as_such()
    {
        IReadOnlyList<WeatherAdvisoryDto> result = Advise([Hour(12, chanceOfRain: 90, precipMm: 0.8)]);

        result.First(advisory => advisory.Kind == WeatherAdvisoryKind.Rain).Text.Should().Contain("уже идёт");
    }

    /// <summary>Snow changes what you wear, not just whether you take an umbrella.</summary>
    [Fact]
    public void Snow_wins_over_rain_when_both_are_forecast()
    {
        IReadOnlyList<HourlyForecast> hourly =
        [
            Hour(14, chanceOfRain: 70, chanceOfSnow: 70),
            Hour(15, chanceOfRain: 70)
        ];

        IReadOnlyList<WeatherAdvisoryDto> result = Advise(hourly);

        result.Should().Contain(advisory => advisory.Kind == WeatherAdvisoryKind.Snow);
        result.Should().NotContain(advisory => advisory.Kind == WeatherAdvisoryKind.Rain);
    }

    [Fact]
    public void A_dry_window_is_stated_explicitly_rather_than_left_silent()
    {
        IReadOnlyList<WeatherAdvisoryDto> result = Advise([Hour(12), Hour(13), Hour(14)]);

        result.Should().Contain(advisory => advisory.Text.Contains("без осадков", StringComparison.Ordinal));
    }

    [Fact]
    public void Rain_beyond_the_horizon_does_not_trigger_an_umbrella_warning()
    {
        // The horizon is twelve hours; a shower tomorrow morning is not an answer to "do I need an umbrella now".
        IReadOnlyList<WeatherAdvisoryDto> result = Advise([Hour(12), Hour(30, chanceOfRain: 90)]);

        result.Should().NotContain(advisory => advisory.Kind == WeatherAdvisoryKind.Rain);
    }

    [Fact]
    public void A_colder_felt_temperature_is_explained_by_the_wind()
    {
        IReadOnlyList<WeatherAdvisoryDto> result = Advise([Hour(12)], Fake.Current(temp: 5, feelsLike: 0, windKph: 25));

        result.Should().Contain(advisory =>
            advisory.Kind == WeatherAdvisoryKind.Wind && advisory.Text.Contains("холоднее", StringComparison.Ordinal));
    }

    [Fact]
    public void A_warmer_felt_temperature_is_explained_by_humidity()
    {
        IReadOnlyList<WeatherAdvisoryDto> result = Advise([Hour(12)], Fake.Current(temp: 28, feelsLike: 33, humidity: 80));

        result.Should().Contain(advisory =>
            advisory.Kind == WeatherAdvisoryKind.Wind && advisory.Text.Contains("теплее", StringComparison.Ordinal));
    }

    [Fact]
    public void Strong_gusts_are_reported_even_when_the_felt_temperature_barely_moves()
    {
        IReadOnlyList<WeatherAdvisoryDto> result = Advise([Hour(12)], Fake.Current(temp: 15, feelsLike: 15, gustKph: 70));

        result.Should().Contain(advisory => advisory.Text.Contains("Порывы", StringComparison.Ordinal));
    }

    [Fact]
    public void A_small_felt_temperature_gap_is_not_worth_a_sentence()
    {
        IReadOnlyList<WeatherAdvisoryDto> result = Advise([Hour(12)], Fake.Current(temp: 15, feelsLike: 14, gustKph: 12));

        result.Should().NotContain(advisory => advisory.Kind == WeatherAdvisoryKind.Wind);
    }

    [Fact]
    public void A_high_ultraviolet_window_is_reported_with_its_boundaries()
    {
        IReadOnlyList<HourlyForecast> hourly =
        [
            Hour(11, uvIndex: 3),
            Hour(12, uvIndex: 7),
            Hour(13, uvIndex: 8),
            Hour(14, uvIndex: 2)
        ];

        IReadOnlyList<WeatherAdvisoryDto> result = Advise(hourly);

        WeatherAdvisoryDto uv = result.First(advisory => advisory.Kind == WeatherAdvisoryKind.Ultraviolet);
        uv.Text.Should().Contain("12:00").And.Contain("14:00");
    }

    [Fact]
    public void A_low_ultraviolet_day_produces_no_ultraviolet_advice()
    {
        IReadOnlyList<WeatherAdvisoryDto> result = Advise([Hour(12, uvIndex: 2), Hour(13, uvIndex: 3)]);

        result.Should().NotContain(advisory => advisory.Kind == WeatherAdvisoryKind.Ultraviolet);
    }

    [Fact]
    public void An_approaching_sunset_is_announced_with_the_time_left()
    {
        DailyForecast today = Fake.Daily(
            new DateOnly(2026, 8, 14),
            astro: new AstroInfo(new TimeOnly(5, 12), new TimeOnly(14, 30), "Full Moon"));

        IReadOnlyList<WeatherAdvisoryDto> result = Advise([Hour(12)], daily: [today]);

        result.First(advisory => advisory.Kind == WeatherAdvisoryKind.Daylight)
            .Text.Should().Contain("14:30").And.Contain("2 ч");
    }

    [Fact]
    public void A_sunset_far_away_is_not_worth_mentioning()
    {
        DailyForecast today = Fake.Daily(
            new DateOnly(2026, 8, 14),
            astro: new AstroInfo(new TimeOnly(5, 12), new TimeOnly(21, 30), "Full Moon"));

        IReadOnlyList<WeatherAdvisoryDto> result = Advise([Hour(12)], daily: [today]);

        result.Should().NotContain(advisory => advisory.Kind == WeatherAdvisoryKind.Daylight);
    }

    [Fact]
    public void After_sunset_the_next_sunrise_is_offered_instead()
    {
        DailyForecast today = Fake.Daily(
            new DateOnly(2026, 8, 14),
            astro: new AstroInfo(new TimeOnly(5, 12), new TimeOnly(10, 0), "Full Moon"));

        IReadOnlyList<WeatherAdvisoryDto> result = Advise([Hour(12)], daily: [today]);

        result.First(advisory => advisory.Kind == WeatherAdvisoryKind.Daylight)
            .Text.Should().Contain("Рассвет").And.Contain("05:12");
    }

    [Fact]
    public void A_missing_astro_block_produces_no_daylight_advice_instead_of_throwing()
    {
        IReadOnlyList<WeatherAdvisoryDto> result = Advise([Hour(12)], daily: [Fake.Daily(new DateOnly(2026, 8, 14))]);

        result.Should().NotContain(advisory => advisory.Kind == WeatherAdvisoryKind.Daylight);
    }

    [Theory]
    [InlineData(-25, "Экстремальный")]
    [InlineData(-12, "мороз")]
    [InlineData(-2, "Мороз")]
    [InlineData(5, "Прохладно")]
    [InlineData(12, "Свежо")]
    [InlineData(20, "Комфортно")]
    [InlineData(26, "Тепло")]
    [InlineData(34, "Жарко")]
    public void Clothing_advice_follows_the_felt_temperature(double feelsLike, string expected)
    {
        IReadOnlyList<WeatherAdvisoryDto> result = Advise([Hour(12)], Fake.Current(temp: feelsLike, feelsLike: feelsLike));

        result.First(advisory => advisory.Kind == WeatherAdvisoryKind.Clothing)
            .Text.Should().ContainEquivalentOf(expected);
    }

    [Fact]
    public void An_empty_forecast_still_produces_usable_advice()
    {
        IReadOnlyList<WeatherAdvisoryDto> result = WeatherAdvisor.Advise(Fake.Current(), [], [], LocalNow);

        result.Should().NotBeEmpty();
        result.Should().Contain(advisory => advisory.Kind == WeatherAdvisoryKind.Clothing);
    }

    [Fact]
    public void Advice_never_contains_an_unformatted_placeholder()
    {
        IReadOnlyList<WeatherAdvisoryDto> result = Advise([Hour(12, chanceOfRain: 90)]);

        result.Should().OnlyContain(advisory => !advisory.Text.Contains('{', StringComparison.Ordinal));
        result.Should().OnlyContain(advisory => advisory.Text.Length > 0);
    }

    private static IReadOnlyList<WeatherAdvisoryDto> Advise(
        IReadOnlyList<HourlyForecast> hourly,
        CurrentWeather? current = null,
        IReadOnlyList<DailyForecast>? daily = null)
    {
        return WeatherAdvisor.Advise(
            current ?? Fake.Current(temp: 18, feelsLike: 18, gustKph: 12),
            hourly,
            daily ?? [],
            LocalNow);
    }

    private static HourlyForecast Hour(int hour, int chanceOfRain = 0, int chanceOfSnow = 0, double precipMm = 0, double uvIndex = 0)
    {
        return Fake.Hour(
            LocalNow.Date.AddHours(hour).ToDateTimeOffset(LocalNow.Offset),
            chanceOfRain: chanceOfRain,
            chanceOfSnow: chanceOfSnow,
            precipMm: precipMm,
            uvIndex: uvIndex);
    }
}

internal static class DateTimeTestExtensions
{
    public static DateTimeOffset ToDateTimeOffset(this DateTime value, TimeSpan offset)
    {
        return new DateTimeOffset(value, offset);
    }
}
