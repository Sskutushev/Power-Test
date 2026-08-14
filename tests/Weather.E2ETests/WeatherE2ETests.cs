using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Weather.E2ETests;

/// <summary>
/// Browser-level journeys through the real application: first paint, failure and recovery, mobile layout,
/// and keyboard reachability.
/// </summary>
[Collection(nameof(WeatherBrowserCollection))]
public sealed class WeatherE2ETests(WeatherBrowserFixture fixture)
{
    [Fact]
    public async Task Dashboard_shows_current_hourly_and_daily_forecast()
    {
        SkipIfBrowserMissing();
        await fixture.StubProviderAsync(success: true);
        IPage page = await OpenAsync();

        await page.Locator(".current-weather").WaitForAsync();

        // The header shows the name the provider resolved, which for this fixture is "Moscow".
        (await page.Locator(".brand__text").InnerTextAsync()).Should().Contain("Moscow");
        (await page.Locator(".temperature").InnerTextAsync()).Should().Contain("22,3");
        (await page.Locator(".hourly-card").CountAsync()).Should().Be(33);
        (await page.Locator(".daily-card").CountAsync()).Should().Be(3);
        (await page.Locator(".skeleton").CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Provider_failure_shows_a_retryable_error_and_recovers()
    {
        SkipIfBrowserMissing();
        await fixture.StubProviderAsync(success: false);
        IPage page = await OpenAsync();

        await page.Locator(".error-state").WaitForAsync();
        (await page.Locator(".error-state").InnerTextAsync()).Should().Contain("Прогноз недоступен");

        await fixture.StubProviderAsync(success: true);
        await page.Locator(".error-state button").ClickAsync();

        await page.Locator(".current-weather").WaitForAsync(new LocatorWaitForOptions { Timeout = 20000 });
        (await page.Locator(".error-state").CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Error_screen_never_exposes_internal_details()
    {
        SkipIfBrowserMissing();
        await fixture.StubProviderAsync(success: false);
        IPage page = await OpenAsync();

        await page.Locator(".error-state").WaitForAsync();
        string body = await page.Locator("body").InnerTextAsync();

        body.Should().NotContainAny("Exception", "Weather.Infrastructure", "Credential", "key=");
    }

    [Fact]
    public async Task Mobile_viewport_keeps_the_page_free_of_horizontal_scroll()
    {
        SkipIfBrowserMissing();
        await fixture.StubProviderAsync(success: true);
        IBrowserContext context = await fixture.Browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 375, Height = 667 },
            Locale = "ru-RU",
            ReducedMotion = ReducedMotion.Reduce
        });
        IPage page = await context.NewPageAsync();
        await page.GotoAsync(fixture.ServerAddress);
        await page.Locator(".current-weather").WaitForAsync();

        int scrollWidth = await page.EvaluateAsync<int>("document.documentElement.scrollWidth");
        int clientWidth = await page.EvaluateAsync<int>("document.documentElement.clientWidth");

        scrollWidth.Should().BeLessThanOrEqualTo(clientWidth + 1, "the hourly strip must scroll inside its own container");

        // The strip itself is still horizontally scrollable, which is the intended mobile affordance.
        int stripScrollWidth = await page.EvaluateAsync<int>("document.querySelector('.hourly-strip').scrollWidth");
        int stripClientWidth = await page.EvaluateAsync<int>("document.querySelector('.hourly-strip').clientWidth");
        stripScrollWidth.Should().BeGreaterThan(stripClientWidth);

        await context.CloseAsync();
    }

    [Fact]
    public async Task Refresh_is_reachable_and_operable_from_the_keyboard()
    {
        SkipIfBrowserMissing();
        await fixture.StubProviderAsync(success: true);
        IPage page = await OpenAsync();
        await page.Locator(".current-weather").WaitForAsync();

        ILocator refresh = page.Locator(".weather-header button");
        await refresh.FocusAsync();

        (await refresh.EvaluateAsync<bool>("element => element === document.activeElement")).Should().BeTrue();

        await page.Keyboard.PressAsync("Enter");
        await page.Locator(".current-weather").WaitForAsync();

        (await page.Locator(".current-weather").CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Theme_choice_survives_a_reload()
    {
        SkipIfBrowserMissing();
        await fixture.StubProviderAsync(success: true);
        IPage page = await OpenAsync();
        await page.Locator(".current-weather").WaitForAsync();

        await page.Locator(".theme-chip", new PageLocatorOptions { HasTextString = "Тёмная" }).ClickAsync();

        // Clicking only dispatches the event; applying the theme takes a Blazor Server round trip, so this
        // has to be a polling assertion. It cannot be WaitForFunctionAsync: that needs eval, which the
        // app's own Content Security Policy forbids.
        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "midnight");

        await page.ReloadAsync();
        await page.Locator(".current-weather").WaitForAsync();

        // After a reload the theme module restores the choice before first paint.
        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "midnight");
        (await page.Locator(".theme-chip[aria-pressed='true']").InnerTextAsync()).Should().Contain("Тёмная");
    }

    [Fact]
    public async Task Api_and_ui_report_the_same_numbers()
    {
        SkipIfBrowserMissing();
        await fixture.StubProviderAsync(success: true);
        IPage page = await OpenAsync();
        await page.Locator(".current-weather").WaitForAsync();

        IAPIResponse response = await page.APIRequest.GetAsync($"{fixture.ServerAddress}/api/weather");
        string json = await response.TextAsync();

        json.Should().Contain("22.3", "the HTTP API and the UI share one use case");
        (await page.Locator(".temperature").InnerTextAsync()).Should().Contain("22,3");
    }

    /// <summary>
    /// A strict Content Security Policy is only useful if the application itself stays inside it. This
    /// catches the usual regression: someone adds an inline script or a CDN asset and the page silently
    /// loses functionality in production while still "working" in development.
    /// </summary>
    [Fact]
    public async Task The_application_never_violates_its_own_content_security_policy()
    {
        SkipIfBrowserMissing();
        await fixture.StubProviderAsync(success: true);

        IPage page = await fixture.NewPageAsync();
        List<string> violations = [];
        page.Console += (_, message) =>
        {
            if (message.Text.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(message.Text);
            }
        };

        IResponse? response = await page.GotoAsync(fixture.ServerAddress);
        await page.Locator(".current-weather").WaitForAsync();
        await page.Locator(".map-shell, .weather-header").First.WaitForAsync();

        response!.Headers["content-security-policy"].Should().Contain("script-src 'self'");
        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task The_dashboard_answers_questions_rather_than_only_reporting_numbers()
    {
        SkipIfBrowserMissing();
        await fixture.StubProviderAsync(success: true);
        IPage page = await OpenAsync();
        await page.Locator(".current-weather").WaitForAsync();

        (await page.Locator(".advisory").CountAsync()).Should().BeGreaterThan(0);
        (await page.Locator(".hourly-chart__canvas").CountAsync()).Should().Be(1);
        (await page.Locator(".hourly-card--now").CountAsync()).Should().Be(1, "the current hour is marked");
    }

    [Fact]
    public async Task A_forecast_day_expands_to_its_hours_and_sun_times()
    {
        SkipIfBrowserMissing();
        await fixture.StubProviderAsync(success: true);
        IPage page = await OpenAsync();
        await page.Locator(".daily-card").First.WaitForAsync();

        ILocator summary = page.Locator(".daily-card__summary").First;
        await Assertions.Expect(summary).ToHaveAttributeAsync("aria-expanded", "false");

        await summary.ClickAsync();

        await Assertions.Expect(summary).ToHaveAttributeAsync("aria-expanded", "true");
        await page.Locator(".daily-card__details").First.WaitForAsync();
        (await page.Locator(".daily-hour").CountAsync()).Should().Be(24);
    }

    [Fact]
    public async Task The_service_worker_registers_and_the_offline_page_is_reachable()
    {
        SkipIfBrowserMissing();
        await fixture.StubProviderAsync(success: true);
        IPage page = await OpenAsync();
        await page.Locator(".current-weather").WaitForAsync();

        IAPIResponse manifest = await page.APIRequest.GetAsync($"{fixture.ServerAddress}/manifest.webmanifest");
        IAPIResponse worker = await page.APIRequest.GetAsync($"{fixture.ServerAddress}/sw.js");
        IAPIResponse offline = await page.APIRequest.GetAsync($"{fixture.ServerAddress}/offline.html");

        manifest.Ok.Should().BeTrue();
        worker.Ok.Should().BeTrue();
        offline.Ok.Should().BeTrue();
        (await manifest.TextAsync()).Should().Contain("\"display\": \"standalone\"");
    }

    private async Task<IPage> OpenAsync()
    {
        IPage page = await fixture.NewPageAsync();
        await page.GotoAsync(fixture.ServerAddress);

        return page;
    }

    private void SkipIfBrowserMissing()
    {
        Assert.SkipWhen(fixture.UnavailableReason is not null, fixture.UnavailableReason ?? string.Empty);
    }
}

/// <summary>Shares one application host and one browser across the whole E2E suite.</summary>
[CollectionDefinition(nameof(WeatherBrowserCollection))]
public sealed class WeatherBrowserCollection : ICollectionFixture<WeatherBrowserFixture>;
