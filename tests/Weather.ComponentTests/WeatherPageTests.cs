using Bunit;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Weather.Application.Common;
using Weather.Application.Weather.GetRegionalWeather;
using Weather.Application.Weather.GetWeatherDashboard;
using Weather.Web.Components.Pages;
using Xunit;

namespace Weather.ComponentTests;

/// <summary>
/// Page-level behaviour: state machine, retry semantics, and the promise that opening the page costs
/// exactly one dashboard query.
/// </summary>
public sealed class WeatherPageTests : BunitContext
{
    public WeatherPageTests()
    {
        // The page hosts JS-backed children (theme, backdrop, intro, map). Loose mode lets them start
        // and no-op, which is exactly how they behave in a browser with the modules blocked.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Cold_start_shows_the_skeleton_until_the_query_completes()
    {
        var sender = FakeSender.Pending();
        Services.AddSingleton<ISender>(sender);

        IRenderedComponent<WeatherPage> page = Render<WeatherPage>();

        page.FindAll(".skeleton").Should().NotBeEmpty();
        page.FindAll(".current-weather").Should().BeEmpty();
        page.FindAll(".error-state").Should().BeEmpty();
    }

    [Fact]
    public void Successful_load_renders_current_hourly_and_daily_sections()
    {
        Services.AddSingleton<ISender>(FakeSender.Returning(WeatherTestData.Dashboard()));

        IRenderedComponent<WeatherPage> page = Render<WeatherPage>();

        page.FindAll(".current-weather").Should().HaveCount(1);
        page.FindAll(".hourly-card").Should().HaveCount(WeatherTestData.Hourly().Count);
        page.FindAll(".daily-card").Should().HaveCount(3);
        page.Markup.Should().Contain("+22,3 °C");
    }

    [Fact]
    public void Opening_the_page_issues_exactly_one_dashboard_query()
    {
        var sender = FakeSender.Returning(WeatherTestData.Dashboard());
        Services.AddSingleton<ISender>(sender);

        Render<WeatherPage>();

        sender.DashboardCalls.Should().Be(1);
    }

    [Fact]
    public void Provider_failure_renders_the_error_state_with_a_human_message()
    {
        Services.AddSingleton<ISender>(FakeSender.Throwing(new WeatherProviderTimeoutException("boom", new TimeoutException())));

        IRenderedComponent<WeatherPage> page = Render<WeatherPage>();

        page.FindAll(".error-state").Should().HaveCount(1);
        page.Markup.Should().Contain("Сервис погоды сейчас не отвечает");
        page.Markup.Should().NotContain("boom");
    }

    [Fact]
    public void Unexpected_failure_is_contained_instead_of_tearing_down_the_circuit()
    {
        Services.AddSingleton<ISender>(FakeSender.Throwing(new InvalidOperationException("unmapped")));

        IRenderedComponent<WeatherPage> page = Render<WeatherPage>();

        page.FindAll(".error-state").Should().HaveCount(1);
        page.Markup.Should().Contain("Не удалось загрузить прогноз");
        page.Markup.Should().NotContain("unmapped");
    }

    [Fact]
    public void Retry_after_a_failure_issues_a_second_query_and_recovers()
    {
        var sender = FakeSender.ThrowingThenReturning(
            new WeatherProviderException(WeatherFailureKind.Provider, "down"),
            WeatherTestData.Dashboard());
        Services.AddSingleton<ISender>(sender);

        IRenderedComponent<WeatherPage> page = Render<WeatherPage>();
        page.Find(".error-state button").Click();

        sender.DashboardCalls.Should().Be(2);
        page.FindAll(".error-state").Should().BeEmpty();
        page.FindAll(".current-weather").Should().HaveCount(1);
    }

    [Fact]
    public void Retry_bypasses_the_cache()
    {
        var sender = FakeSender.Returning(WeatherTestData.Dashboard());
        Services.AddSingleton<ISender>(sender);

        IRenderedComponent<WeatherPage> page = Render<WeatherPage>();
        page.Find(".weather-header button").Click();

        sender.DashboardQueries.Should().HaveCount(2);
        sender.DashboardQueries[0].BypassCache.Should().BeFalse();
        sender.DashboardQueries[1].BypassCache.Should().BeTrue();
    }

    [Fact]
    public void Double_click_on_refresh_does_not_start_a_second_parallel_request()
    {
        var sender = FakeSender.Returning(WeatherTestData.Dashboard());
        Services.AddSingleton<ISender>(sender);

        IRenderedComponent<WeatherPage> page = Render<WeatherPage>();

        // From here the query never completes, so the second click lands while the first is still
        // in flight — the situation a real double click creates.
        sender.Reset();
        sender.PendNextRequests = true;

        page.Find(".weather-header button").Click();
        page.Find(".weather-header button").Click();

        sender.DashboardCalls.Should().Be(1);
    }

    [Fact]
    public void Stale_data_is_surfaced_without_hiding_the_forecast()
    {
        Services.AddSingleton<ISender>(FakeSender.Returning(WeatherTestData.Dashboard(isStale: true)));

        IRenderedComponent<WeatherPage> page = Render<WeatherPage>();

        page.Markup.Should().Contain("Данные могут быть неактуальны");
        page.FindAll(".current-weather").Should().HaveCount(1);
    }

    [Fact]
    public void Status_line_is_announced_politely()
    {
        Services.AddSingleton<ISender>(FakeSender.Returning(WeatherTestData.Dashboard()));

        IRenderedComponent<WeatherPage> page = Render<WeatherPage>();

        page.Find(".status-line").GetAttribute("aria-live").Should().Be("polite");
    }

    [Fact]
    public void Disposing_the_page_cancels_an_in_flight_query()
    {
        var sender = FakeSender.Pending();
        Services.AddSingleton<ISender>(sender);

        IRenderedComponent<WeatherPage> page = Render<WeatherPage>();
        page.Instance.Dispose();

        sender.LastToken.IsCancellationRequested.Should().BeTrue();
    }

    /// <summary>
    /// Minimal in-memory <see cref="ISender"/>. A fake is used rather than a mocking framework because the
    /// tests care about call counts and the queries that were sent, both of which a fake records plainly.
    /// </summary>
    private sealed class FakeSender : ISender
    {
        private readonly Queue<Func<WeatherDashboardDto>> dashboardResults = new();
        private TaskCompletionSource<WeatherDashboardDto>? pending;

        /// <summary>Makes every following query hang, so callers can observe in-flight behaviour.</summary>
        public bool PendNextRequests { get; set; }

        public int DashboardCalls { get; private set; }

        public List<GetWeatherDashboardQuery> DashboardQueries { get; } = [];

        public CancellationToken LastToken { get; private set; }

        public static FakeSender Returning(WeatherDashboardDto dashboard)
        {
            FakeSender sender = new();
            sender.dashboardResults.Enqueue(() => dashboard);
            sender.dashboardResults.Enqueue(() => dashboard);
            sender.dashboardResults.Enqueue(() => dashboard);

            return sender;
        }

        public static FakeSender Throwing(Exception exception)
        {
            FakeSender sender = new();
            sender.dashboardResults.Enqueue(() => throw exception);

            return sender;
        }

        public static FakeSender ThrowingThenReturning(Exception exception, WeatherDashboardDto dashboard)
        {
            FakeSender sender = new();
            sender.dashboardResults.Enqueue(() => throw exception);
            sender.dashboardResults.Enqueue(() => dashboard);

            return sender;
        }

        public static FakeSender Pending()
        {
            return new FakeSender { PendNextRequests = true };
        }

        public void Reset()
        {
            DashboardCalls = 0;
            DashboardQueries.Clear();
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;

            switch (request)
            {
                case GetWeatherDashboardQuery dashboardQuery:
                    DashboardCalls++;
                    DashboardQueries.Add(dashboardQuery);

                    if (PendNextRequests)
                    {
                        pending ??= new TaskCompletionSource<WeatherDashboardDto>();
                        return (Task<TResponse>)(object)pending.Task;
                    }

                    return dashboardResults.Count > 0
                        ? Task.FromResult((TResponse)(object)dashboardResults.Dequeue()())
                        : throw new InvalidOperationException("Unexpected dashboard query.");

                case GetRegionalWeatherQuery:
                    return Task.FromResult((TResponse)(object)WeatherTestData.Region());

                default:
                    throw new NotSupportedException($"Unexpected request {request.GetType().Name}.");
            }
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
