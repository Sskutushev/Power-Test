using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Weather.Application.Abstractions;
using Weather.Application.Common;
using Weather.Application.Weather;
using Weather.Application.Weather.GetRegionalWeather;
using Weather.Domain;
using Xunit;

namespace Weather.Application.Tests;

/// <summary>
/// The territory map use case: configuration in, UI-ready markers out, and no provider traffic when the
/// feature is switched off.
/// </summary>
public sealed class GetRegionalWeatherQueryHandlerTests
{
    [Fact]
    public async Task Handler_maps_every_configured_point_to_a_marker()
    {
        FakeRegionalProvider provider = new(Snapshot());
        GetRegionalWeatherQueryHandler handler = CreateHandler(provider, Options());

        RegionalWeatherDto result = await handler.Handle(new GetRegionalWeatherQuery(), TestContext.Current.CancellationToken);

        result.Points.Should().HaveCount(2);
        result.Points[0].Name.Should().Be("Москва");
        result.Points[0].TempC.Should().Be(21);
        result.Points[0].Latitude.Should().Be(55.7522);
        result.CenterLatitude.Should().Be(55.7522);
        result.CenterLongitude.Should().Be(37.6156);
        result.Zoom.Should().Be(6);
    }

    [Fact]
    public async Task Handler_passes_the_configured_points_to_the_provider()
    {
        FakeRegionalProvider provider = new(Snapshot());
        GetRegionalWeatherQueryHandler handler = CreateHandler(provider, Options());

        await handler.Handle(new GetRegionalWeatherQuery(), TestContext.Current.CancellationToken);

        provider.RequestedPoints.Should().HaveCount(2);
        provider.RequestedPoints[1].City.Should().Be("Тверь");
        provider.RequestedPoints[1].Coordinates.Should().Be(new GeoPoint(56.8587, 35.9176));
    }

    [Fact]
    public async Task Disabled_map_never_reaches_the_provider()
    {
        FakeRegionalProvider provider = new(Snapshot());
        WeatherOptions options = Options();
        WeatherOptions disabled = new()
        {
            Location = options.Location,
            Latitude = options.Latitude,
            Longitude = options.Longitude,
            Region = new RegionOptions { Enabled = false, Zoom = 6, Points = options.Region.Points }
        };
        GetRegionalWeatherQueryHandler handler = CreateHandler(provider, disabled);

        RegionalWeatherDto result = await handler.Handle(new GetRegionalWeatherQuery(), TestContext.Current.CancellationToken);

        result.Points.Should().BeEmpty();
        provider.Calls.Should().Be(0);
    }

    [Fact]
    public async Task No_configured_points_means_no_provider_call()
    {
        FakeRegionalProvider provider = new(Snapshot());
        GetRegionalWeatherQueryHandler handler = CreateHandler(provider, new WeatherOptions());

        RegionalWeatherDto result = await handler.Handle(new GetRegionalWeatherQuery(), TestContext.Current.CancellationToken);

        result.Points.Should().BeEmpty();
        provider.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Stale_results_are_surfaced_to_the_caller()
    {
        FakeRegionalProvider provider = new(Snapshot() with { IsStale = true });
        GetRegionalWeatherQueryHandler handler = CreateHandler(provider, Options());

        RegionalWeatherDto result = await handler.Handle(new GetRegionalWeatherQuery(), TestContext.Current.CancellationToken);

        result.IsStale.Should().BeTrue();
    }

    [Fact]
    public async Task Provider_failures_are_not_swallowed()
    {
        FakeRegionalProvider provider = new(new WeatherProviderException(WeatherFailureKind.Provider, "down"));
        GetRegionalWeatherQueryHandler handler = CreateHandler(provider, Options());

        Func<Task> act = () => handler.Handle(new GetRegionalWeatherQuery(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WeatherProviderException>();
    }

    [Fact]
    public async Task Cancellation_short_circuits_before_the_provider_is_touched()
    {
        FakeRegionalProvider provider = new(Snapshot());
        GetRegionalWeatherQueryHandler handler = CreateHandler(provider, Options());
        using CancellationTokenSource source = new();
        await source.CancelAsync();

        Func<Task> act = () => handler.Handle(new GetRegionalWeatherQuery(), source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        provider.Calls.Should().Be(0);
    }

    private static GetRegionalWeatherQueryHandler CreateHandler(FakeRegionalProvider provider, WeatherOptions options)
    {
        return new GetRegionalWeatherQueryHandler(
            provider,
            Microsoft.Extensions.Options.Options.Create(options),
            new FakeTimeProvider(new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero)));
    }

    private static WeatherOptions Options()
    {
        return new WeatherOptions
        {
            Region = new RegionOptions
            {
                Enabled = true,
                Zoom = 6,
                Points =
                [
                    new RegionPointOptions { Name = "Москва", Latitude = 55.7522, Longitude = 37.6156 },
                    new RegionPointOptions { Name = "Тверь", Latitude = 56.8587, Longitude = 35.9176 }
                ]
            }
        };
    }

    private static RegionalWeatherSnapshot Snapshot()
    {
        WeatherCondition condition = new("Ясно", "https://cdn.weatherapi.com/icon.png", 1000);

        return new RegionalWeatherSnapshot(
        [
            new RegionalWeatherPoint("Москва", new GeoPoint(55.7522, 37.6156), new Temperature(21), new Temperature(20), condition, 5, 50),
            new RegionalWeatherPoint("Тверь", new GeoPoint(56.8587, 35.9176), new Temperature(19), new Temperature(18), condition, 6, 55)
        ]);
    }

    private sealed class FakeRegionalProvider : IRegionalWeatherProvider
    {
        private readonly RegionalWeatherSnapshot? snapshot;
        private readonly Exception? failure;

        public FakeRegionalProvider(RegionalWeatherSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public FakeRegionalProvider(Exception failure)
        {
            this.failure = failure;
        }

        public int Calls { get; private set; }

        public IReadOnlyList<Location> RequestedPoints { get; private set; } = [];

        public Task<RegionalWeatherSnapshot> GetAsync(IReadOnlyList<Location> points, bool bypassCache, CancellationToken cancellationToken)
        {
            Calls++;
            RequestedPoints = points;
            cancellationToken.ThrowIfCancellationRequested();

            return failure is not null
                ? throw failure
                : Task.FromResult(snapshot!);
        }
    }
}
