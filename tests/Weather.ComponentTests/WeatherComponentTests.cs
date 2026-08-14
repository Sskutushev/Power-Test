using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Weather.Application.Common;
using Weather.Application.Weather.GetWeatherDashboard;
using Weather.Web.Components.Weather;
using Xunit;

namespace Weather.ComponentTests;

/// <summary>
/// Presentation-level tests. They assert behaviour and accessible output, never CSS class internals.
/// </summary>
public sealed class WeatherComponentTests : BunitContext
{
    public WeatherComponentTests()
    {
        // The hourly strip enhances itself with a JS module for drag-scrolling and the hover readout.
        // Loose mode lets it no-op, which is exactly how it behaves when the module cannot load.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Current_weather_card_renders_temperature_condition_and_feels_like()
    {
        IRenderedComponent<CurrentWeatherCard> component = Render<CurrentWeatherCard>(parameters => parameters
            .Add(card => card.Current, WeatherTestData.Current()));

        component.Markup.Should().Contain("+22,3 °C");
        component.Markup.Should().Contain("Переменная облачность");
        component.Markup.Should().Contain("Ощущается как +23,1 °C");
    }

    [Fact]
    public void Current_weather_icon_is_decorative_because_the_condition_text_is_already_rendered()
    {
        IRenderedComponent<CurrentWeatherCard> component = Render<CurrentWeatherCard>(parameters => parameters
            .Add(card => card.Current, WeatherTestData.Current()));

        IElement icon = component.Find("img");

        icon.GetAttribute("alt").Should().BeEmpty();
        icon.GetAttribute("aria-hidden").Should().Be("true");
    }

    [Fact]
    public void Missing_provider_icon_falls_back_instead_of_rendering_a_broken_image()
    {
        IRenderedComponent<CurrentWeatherCard> component = Render<CurrentWeatherCard>(parameters => parameters
            .Add(card => card.Current, WeatherTestData.Current(WeatherTestData.Condition(icon: null))));

        component.FindAll("img").Should().BeEmpty();
        component.Markup.Should().Contain("weather-icon--fallback");
    }

    [Fact]
    public void Metrics_grid_converts_pressure_to_millimetres_of_mercury()
    {
        IRenderedComponent<WeatherMetricsGrid> component = Render<WeatherMetricsGrid>(parameters => parameters
            .Add(grid => grid.Current, WeatherTestData.Current()));

        component.Markup.Should().Contain("759 мм рт. ст.");
        component.Markup.Should().Contain("44 %");
        component.Markup.Should().Contain("9,4 км/ч");
        component.Markup.Should().Contain("умеренный");
    }

    [Fact]
    public void Hourly_strip_renders_one_card_per_entry()
    {
        IRenderedComponent<HourlyForecastStrip> component = RenderHourly(WeatherTestData.Hourly());

        component.FindAll(".hourly-card").Should().HaveCount(5);
    }

    [Fact]
    public void Hourly_strip_marks_the_first_hour_of_the_next_day()
    {
        IRenderedComponent<HourlyForecastStrip> component = RenderHourly(WeatherTestData.Hourly());

        IReadOnlyList<IElement> badges = component.FindAll(".hourly-card__badge");

        badges.Should().HaveCount(1);
        badges[0].TextContent.Trim().Should().Be("Завтра");
    }

    [Fact]
    public void Hourly_strip_stays_usable_when_the_provider_returns_nothing()
    {
        IRenderedComponent<HourlyForecastStrip> component = RenderHourly([]);

        component.FindAll(".hourly-card").Should().BeEmpty();
        component.Markup.Should().Contain("Почасовые данные сейчас недоступны");
    }

    [Fact]
    public void Hourly_strip_is_reachable_from_the_keyboard()
    {
        IRenderedComponent<HourlyForecastStrip> component = RenderHourly(WeatherTestData.Hourly());

        component.Find(".hourly-strip").GetAttribute("tabindex").Should().Be("0");
    }

    [Fact]
    public void Daily_list_renders_three_days_with_relative_labels()
    {
        IRenderedComponent<DailyForecastList> component = Render<DailyForecastList>(parameters => parameters
            .Add(list => list.Items, WeatherTestData.Daily())
            .Add(list => list.Today, new DateOnly(2026, 8, 14)));

        component.FindAll(".daily-card").Should().HaveCount(3);
        component.Markup.Should().Contain("Сегодня");
        component.Markup.Should().Contain("Завтра");
    }

    [Fact]
    public void Error_state_shows_the_message_and_disables_retry_while_pending()
    {
        IRenderedComponent<WeatherErrorState> component = Render<WeatherErrorState>(parameters => parameters
            .Add(state => state.Kind, WeatherFailureKind.Timeout)
            .Add(state => state.Message, "Сервис погоды сейчас не отвечает.")
            .Add(state => state.IsRetrying, true)
            .Add(state => state.OnRetry, () => { }));

        component.Markup.Should().Contain("Сервис погоды сейчас не отвечает.");
        component.Find("button").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Error_state_headline_reflects_the_failure_kind()
    {
        IRenderedComponent<WeatherErrorState> component = Render<WeatherErrorState>(parameters => parameters
            .Add(state => state.Kind, WeatherFailureKind.RateLimit)
            .Add(state => state.Message, "Слишком много запросов.")
            .Add(state => state.OnRetry, () => { }));

        component.Find(".error-state__title").TextContent.Should().Contain("Слишком много запросов");
    }

    [Fact]
    public void Error_state_retry_raises_the_callback()
    {
        int calls = 0;

        IRenderedComponent<WeatherErrorState> component = Render<WeatherErrorState>(parameters => parameters
            .Add(state => state.Kind, WeatherFailureKind.Provider)
            .Add(state => state.Message, "Сервис недоступен.")
            .Add(state => state.OnRetry, () => calls++));

        component.Find("button").Click();

        calls.Should().Be(1);
    }

    [Fact]
    public void Error_state_never_leaks_internal_details()
    {
        IRenderedComponent<WeatherErrorState> component = Render<WeatherErrorState>(parameters => parameters
            .Add(state => state.Kind, WeatherFailureKind.Auth)
            .Add(state => state.Message, "Сервис погоды недоступен. Мы уже разбираемся.")
            .Add(state => state.OnRetry, () => { }));

        component.Markup.Should().NotContainAny("Exception", "   at ", "Credential", "key=");
    }

    [Fact]
    public void Header_disables_refresh_while_a_request_is_in_flight()
    {
        IRenderedComponent<WeatherHeader> component = Render<WeatherHeader>(parameters => parameters
            .Add(header => header.Data, WeatherTestData.Dashboard())
            .Add(header => header.IsRefreshing, true)
            .Add(header => header.OnRefresh, () => { }));

        component.Find("button").HasAttribute("disabled").Should().BeTrue();
        component.Markup.Should().Contain("Москва");
    }

    private IRenderedComponent<HourlyForecastStrip> RenderHourly(IReadOnlyList<HourlyForecastDto> items)
    {
        return Render<HourlyForecastStrip>(parameters => parameters
            .Add(strip => strip.Items, items)
            .Add(strip => strip.LocalNow, WeatherTestData.LocalNow));
    }
}
