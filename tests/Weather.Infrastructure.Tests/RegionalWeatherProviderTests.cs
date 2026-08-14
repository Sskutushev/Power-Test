using System.Net;
using FluentAssertions;
using Weather.Application.Abstractions;
using Weather.Application.Common;
using Weather.Domain;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Weather.Infrastructure.Tests;

/// <summary>
/// The territory sweep costs one provider call per map point, so its failure semantics differ from the
/// dashboard: a single bad point must not blank the map, but a fully dead provider still has to surface.
/// </summary>
public sealed class RegionalWeatherProviderTests
{
    private static readonly Location[] Points =
    [
        new("Москва", "Europe/Moscow", new GeoPoint(55.7522, 37.6156)),
        new("Тверь", "Europe/Moscow", new GeoPoint(56.8587, 35.9176)),
        new("Рязань", "Europe/Moscow", new GeoPoint(54.6269, 39.6916))
    ];

    [Fact]
    public async Task All_configured_points_are_resolved()
    {
        await using var host = WeatherApiTestHost.Create();
        StubCurrent(host.Server, HttpStatusCode.OK, WeatherApiFixtures.CurrentSuccess);

        RegionalWeatherSnapshot snapshot = await host.RegionalProvider.GetAsync(Points, bypassCache: true, TestContext.Current.CancellationToken);

        snapshot.Points.Should().HaveCount(3);
        snapshot.Points.Select(point => point.Name).Should().Equal("Москва", "Тверь", "Рязань");
        snapshot.IsStale.Should().BeFalse();
    }

    [Fact]
    public async Task Configured_names_and_coordinates_win_over_the_provider_resolution()
    {
        await using var host = WeatherApiTestHost.Create();
        StubCurrent(host.Server, HttpStatusCode.OK, WeatherApiFixtures.CurrentSuccess);

        RegionalWeatherSnapshot snapshot = await host.RegionalProvider.GetAsync(Points, bypassCache: true, TestContext.Current.CancellationToken);

        RegionalWeatherPoint tver = snapshot.Points.Single(point => string.Equals(point.Name, "Тверь", StringComparison.Ordinal));
        tver.Coordinates.Should().Be(new GeoPoint(56.8587, 35.9176));
        tver.Temp.Celsius.Should().Be(22.3);
    }

    [Fact]
    public async Task One_point_is_queried_per_configured_location()
    {
        await using var host = WeatherApiTestHost.Create();
        StubCurrent(host.Server, HttpStatusCode.OK, WeatherApiFixtures.CurrentSuccess);

        await host.RegionalProvider.GetAsync(Points, bypassCache: true, TestContext.Current.CancellationToken);

        host.Server.LogEntries.Should().HaveCount(3);
    }

    [Fact]
    public async Task An_empty_point_list_never_touches_the_provider()
    {
        await using var host = WeatherApiTestHost.Create();
        StubCurrent(host.Server, HttpStatusCode.OK, WeatherApiFixtures.CurrentSuccess);

        RegionalWeatherSnapshot snapshot = await host.RegionalProvider.GetAsync([], bypassCache: true, TestContext.Current.CancellationToken);

        snapshot.Points.Should().BeEmpty();
        host.Server.LogEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task A_dead_provider_surfaces_the_classified_failure()
    {
        await using var host = WeatherApiTestHost.Create();
        StubCurrent(host.Server, HttpStatusCode.InternalServerError, "{}");

        Func<Task> act = () => host.RegionalProvider.GetAsync(Points, bypassCache: true, TestContext.Current.CancellationToken);

        WeatherProviderException exception = (await act.Should().ThrowAsync<WeatherProviderException>()).Which;
        exception.Kind.Should().Be(WeatherFailureKind.Provider);
    }

    [Fact]
    public async Task Cancellation_propagates_instead_of_being_swallowed_per_point()
    {
        await using var host = WeatherApiTestHost.Create();
        host.Server
            .Given(Request.Create().WithPath("/v1/current.json").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK).WithBody("{}").WithDelay(TimeSpan.FromSeconds(10)));
        using CancellationTokenSource source = new(TimeSpan.FromMilliseconds(200));

        Func<Task> act = () => host.RegionalProvider.GetAsync(Points, bypassCache: true, source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Concurrency_stays_within_the_configured_bound()
    {
        await using var host = WeatherApiTestHost.Create(settings => settings["WeatherApi:MaxRegionConcurrency"] = "1");
        StubCurrent(host.Server, HttpStatusCode.OK, WeatherApiFixtures.CurrentSuccess, TimeSpan.FromMilliseconds(120));

        Location[] many = [.. Points, .. Points];
        DateTimeOffset started = DateTimeOffset.UtcNow;

        await host.RegionalProvider.GetAsync(many, bypassCache: true, TestContext.Current.CancellationToken);

        // Six serialised 120 ms calls cannot finish in less than half a second; a parallel run would.
        (DateTimeOffset.UtcNow - started).Should().BeGreaterThan(TimeSpan.FromMilliseconds(500));
    }

    private static void StubCurrent(WireMockServer server, HttpStatusCode status, string body, TimeSpan? delay = null)
    {
        IResponseBuilder response = Response.Create()
            .WithStatusCode(status)
            .WithHeader("Content-Type", "application/json")
            .WithBody(body);

        if (delay is not null)
        {
            response = response.WithDelay(delay.Value);
        }

        server.Given(Request.Create().WithPath("/v1/current.json").UsingGet()).RespondWith(response);
    }
}
