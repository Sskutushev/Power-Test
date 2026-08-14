using Bunit;
using FluentAssertions;
using Weather.Application.Common;
using Weather.Application.Weather.GetWeatherDashboard;
using Weather.Web.Components.Weather;
using Xunit;

namespace Weather.ComponentTests;

public sealed class WeatherComponentTests : BunitContext
{
    [Fact]
    public void Current_weather_card_renders_temperature_condition_and_accessible_icon()
    {
        CurrentWeatherDto current = BuildDashboard().Current;

        IRenderedComponent<CurrentWeatherCard> component = Render<CurrentWeatherCard>(parameters => parameters
            .Add(component => component.Current, current));

        component.Markup.Should().Contain("+22,3");
        component.Markup.Should().Contain("Переменная облачность");
        component.Find("img").GetAttribute("alt").Should().Be("Переменная облачность");
    }

    [Fact]
    public void Hourly_forecast_strip_renders_one_card_per_hour()
    {
        WeatherDashboardDto dashboard = BuildDashboard();

        IRenderedComponent<HourlyForecastStrip> component = Render<HourlyForecastStrip>(parameters => parameters
            .Add(component => component.Items, dashboard.Hourly));

        component.FindAll(".hourly-card").Should().HaveCount(3);
    }

    [Fact]
    public void Daily_forecast_list_renders_three_days()
    {
        WeatherDashboardDto dashboard = BuildDashboard();

        IRenderedComponent<DailyForecastList> component = Render<DailyForecastList>(parameters => parameters
            .Add(component => component.Items, dashboard.Daily));

        component.FindAll(".daily-card").Should().HaveCount(3);
    }

    [Fact]
    public void Error_state_shows_message_and_disables_retry_while_pending()
    {
        IRenderedComponent<WeatherErrorState> component = Render<WeatherErrorState>(parameters => parameters
            .Add(component => component.Kind, WeatherFailureKind.Timeout)
            .Add(component => component.Message, "Сервис погоды сейчас не отвечает. Попробуйте ещё раз.")
            .Add(component => component.IsRetrying, true)
            .Add(component => component.OnRetry, () => Task.CompletedTask));

        component.Markup.Should().Contain("Сервис погоды сейчас не отвечает");
        component.Find("button").HasAttribute("disabled").Should().BeTrue();
    }

    private static WeatherDashboardDto BuildDashboard()
    {
        WeatherConditionDto condition = new("Переменная облачность", "https://cdn.weatherapi.com/weather/64x64/day/116.png", 1003);
        CurrentWeatherDto current = new(22.3, 23.1, 44, 9.4, 1012, 4, condition, new DateTimeOffset(2026, 8, 14, 15, 15, 0, TimeSpan.FromHours(3)));
        HourlyForecastDto[] hourly =
        [
            new(new DateTimeOffset(2026, 8, 14, 15, 0, 0, TimeSpan.FromHours(3)), 22, condition, 10, 4),
            new(new DateTimeOffset(2026, 8, 14, 16, 0, 0, TimeSpan.FromHours(3)), 21, condition, 10, 4),
            new(new DateTimeOffset(2026, 8, 14, 17, 0, 0, TimeSpan.FromHours(3)), 20, condition, 10, 4)
        ];
        DailyForecastDto[] daily =
        [
            new(new DateOnly(2026, 8, 14), 12, 24, condition, 20),
            new(new DateOnly(2026, 8, 15), 13, 25, condition, 20),
            new(new DateOnly(2026, 8, 16), 14, 26, condition, 20)
        ];

        return new WeatherDashboardDto(
            new LocationDto("Moscow", "Europe/Moscow"),
            current,
            hourly,
            daily,
            new DateTimeOffset(2026, 8, 14, 12, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 14, 15, 30, 0, TimeSpan.FromHours(3)),
            false,
            null);
    }
}
