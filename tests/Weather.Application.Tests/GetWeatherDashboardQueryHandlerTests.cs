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

    /// <summary>
    /// A distributed cache outlives a deployment. An entry written before a field existed deserialises
    /// with that field defaulted, so the mapping must degrade rather than throw — this exact shape caused
    /// a 500 on a live instance after the astro block was added.
    /// </summary>
    [Fact]
    public async Task Handler_tolerates_a_snapshot_missing_fields_added_by_a_later_version()
    {
        WeatherSnapshot legacy = BuildSnapshot(new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.FromHours(3))) with
        {
            Days =
            [
                new DayForecast(
                    new DateOnly(2026, 8, 14),
                    Hours: null!,
                    Daily: Fake.Daily(new DateOnly(2026, 8, 14)) with { Astro = null! })
            ]
        };
        GetWeatherDashboardQueryHandler handler = CreateHandler(new FakeWeatherProvider(legacy));

        WeatherDashboardDto result = await handler.Handle(new GetWeatherDashboardQuery(), TestContext.Current.CancellationToken);

        result.Daily.Should().ContainSingle();
        result.Daily[0].Astro.Sunrise.Should().BeNull();
        result.Daily[0].Hours.Should().BeEmpty();
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
            Days = [Fake.Day(new DateOnly(2026, 8, 14), [])]
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

        return new WeatherSnapshot(
            location,
            Fake.Current(),
            [
                BuildDay(new DateOnly(2026, 8, 14)),
                BuildDay(new DateOnly(2026, 8, 15)),
                BuildDay(new DateOnly(2026, 8, 16))
            ],
            localNow);
    }

    private static DayForecast BuildDay(DateOnly date)
    {
        return Fake.Day(date, Fake.Hours(date));
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
