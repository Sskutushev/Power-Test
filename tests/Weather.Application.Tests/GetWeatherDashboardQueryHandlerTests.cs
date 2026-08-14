using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Weather.Application.Abstractions;
using Weather.Application.Common;
using Weather.Application.Weather;
using Weather.Application.Weather.GetWeatherDashboard;
using Weather.Domain;
using Xunit;

namespace Weather.Application.Tests;

public sealed class GetWeatherDashboardQueryHandlerTests
{
    [Fact]
    public async Task Handler_builds_dashboard_and_filters_hourly_forecast()
    {
        FakeWeatherProvider provider = new(BuildSnapshot(new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.FromHours(3))));
        GetWeatherDashboardQueryHandler handler = CreateHandler(provider);

        WeatherDashboardDto result = await handler.Handle(new GetWeatherDashboardQuery(), CancellationToken.None);

        result.Location.City.Should().Be("Moscow");
        result.Hourly.Should().HaveCount(38);
        result.Daily.Should().HaveCount(3);
        provider.ForecastDays.Should().Be(3);
    }

    [Fact]
    public async Task Handler_uses_the_configured_location_by_default()
    {
        FakeWeatherProvider provider = new(BuildSnapshot(new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.FromHours(3))));
        GetWeatherDashboardQueryHandler handler = CreateHandler(provider);

        await handler.Handle(new GetWeatherDashboardQuery(), TestContext.Current.CancellationToken);

        provider.RequestedLocation!.Coordinates.Should().Be(new GeoPoint(55.7522, 37.6156));
    }

    /// <summary>
    /// The assignment fixes the location to Moscow, so the visitor's own coordinates only take effect when
    /// both are supplied explicitly — a half-filled override must never silently move the forecast.
    /// </summary>
    [Fact]
    public async Task Handler_honours_an_explicit_coordinate_override()
    {
        FakeWeatherProvider provider = new(BuildSnapshot(new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.FromHours(3))));
        GetWeatherDashboardQueryHandler handler = CreateHandler(provider);

        await handler.Handle(new GetWeatherDashboardQuery(Latitude: 59.9375, Longitude: 30.3086), TestContext.Current.CancellationToken);

        provider.RequestedLocation!.Coordinates.Should().Be(new GeoPoint(59.9375, 30.3086));
    }

    [Theory]
    [InlineData(59.9375, null)]
    [InlineData(null, 30.3086)]
    public async Task Handler_ignores_a_half_supplied_override(double? latitude, double? longitude)
    {
        FakeWeatherProvider provider = new(BuildSnapshot(new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.FromHours(3))));
        GetWeatherDashboardQueryHandler handler = CreateHandler(provider);

        await handler.Handle(new GetWeatherDashboardQuery(Latitude: latitude, Longitude: longitude), TestContext.Current.CancellationToken);

        provider.RequestedLocation!.Coordinates.Should().Be(new GeoPoint(55.7522, 37.6156));
    }

    [Fact]
    public async Task Handler_does_not_swallow_provider_timeout()
    {
        FakeWeatherProvider provider = new(new WeatherProviderTimeoutException("Timeout", new TimeoutException()));
        GetWeatherDashboardQueryHandler handler = CreateHandler(provider);

        Func<Task> act = () => handler.Handle(new GetWeatherDashboardQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<WeatherProviderTimeoutException>();
    }

    [Fact]
    public async Task Handler_propagates_cancellation_token_to_provider()
    {
        FakeWeatherProvider provider = new(BuildSnapshot(new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.FromHours(3))));
        GetWeatherDashboardQueryHandler handler = CreateHandler(provider);
        using CancellationTokenSource source = new();
        await source.CancelAsync();

        Func<Task> act = () => handler.Handle(new GetWeatherDashboardQuery(), source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        provider.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Handler_returns_empty_hourly_when_provider_day_has_no_hours()
    {
        WeatherSnapshot snapshot = BuildSnapshot(new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.FromHours(3))) with
        {
            Days = [BuildDay(new DateOnly(2026, 8, 14), [])]
        };
        GetWeatherDashboardQueryHandler handler = CreateHandler(new FakeWeatherProvider(snapshot));

        WeatherDashboardDto result = await handler.Handle(new GetWeatherDashboardQuery(), CancellationToken.None);

        result.Hourly.Should().BeEmpty();
    }

    [Fact]
    public async Task Handler_uses_time_provider_fallback_when_provider_localtime_is_missing()
    {
        DateTimeOffset fallback = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(fallback);
        WeatherSnapshot snapshot = BuildSnapshot(null);
        GetWeatherDashboardQueryHandler handler = CreateHandler(new FakeWeatherProvider(snapshot), timeProvider);

        WeatherDashboardDto result = await handler.Handle(new GetWeatherDashboardQuery(), CancellationToken.None);

        result.LocalNow.Should().Be(fallback);
    }

    private static GetWeatherDashboardQueryHandler CreateHandler(FakeWeatherProvider provider, TimeProvider? timeProvider = null)
    {
        WeatherOptions options = new();
        return new GetWeatherDashboardQueryHandler(provider, Options.Create(options), timeProvider ?? TimeProvider.System);
    }

    private static WeatherSnapshot BuildSnapshot(DateTimeOffset? localNow)
    {
        Location location = new("Moscow", "Europe/Moscow", new GeoPoint(55.7522, 37.6156));
        WeatherCondition condition = new("Ясно", "https://cdn.weatherapi.com/icon.png", 1000);
        CurrentWeather current = new(new Temperature(20), new Temperature(21), 40, 8, 1012, 3, condition, new DateTimeOffset(2026, 8, 14, 10, 0, 0, TimeSpan.FromHours(3)));

        return new WeatherSnapshot(
            location,
            current,
            [
                BuildDay(new DateOnly(2026, 8, 14), BuildHours(new DateOnly(2026, 8, 14), condition)),
                BuildDay(new DateOnly(2026, 8, 15), BuildHours(new DateOnly(2026, 8, 15), condition)),
                BuildDay(new DateOnly(2026, 8, 16), BuildHours(new DateOnly(2026, 8, 16), condition))
            ],
            localNow);
    }

    private static DayForecast BuildDay(DateOnly date, IReadOnlyList<HourlyForecast> hours)
    {
        WeatherCondition condition = new("Ясно", null, 1000);
        DailyForecast daily = new(date, new Temperature(10), new Temperature(20), condition, 0);
        return new DayForecast(date, hours, daily);
    }

    private static IReadOnlyList<HourlyForecast> BuildHours(DateOnly date, WeatherCondition condition)
    {
        return Enumerable
            .Range(0, 24)
            .Select(hour => new HourlyForecast(new DateTimeOffset(date.Year, date.Month, date.Day, hour, 0, 0, TimeSpan.FromHours(3)), new Temperature(hour), condition, 0, 1))
            .ToArray();
    }

    private sealed class FakeWeatherProvider : IWeatherProvider
    {
        private readonly WeatherSnapshot? snapshot;
        private readonly Exception? exception;

        public FakeWeatherProvider(WeatherSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public FakeWeatherProvider(Exception exception)
        {
            this.exception = exception;
        }

        public int Calls { get; private set; }

        public int ForecastDays { get; private set; }

        public Location? RequestedLocation { get; private set; }

        public Task<WeatherSnapshot> GetAsync(Location location, int forecastDays, bool bypassCache, CancellationToken cancellationToken)
        {
            Calls++;
            ForecastDays = forecastDays;
            RequestedLocation = location;
            cancellationToken.ThrowIfCancellationRequested();

            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(snapshot!);
        }
    }
}
