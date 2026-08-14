using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Weather.Application.Abstractions;
using Weather.Application.Common;
using Weather.Domain;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Weather.Infrastructure.Tests;

/// <summary>
/// Provider contract tests: real HttpClient, real resilience pipeline, real mapping, stubbed peer.
/// These are the tests that prove the WeatherAPI JSON contract is understood correctly.
/// </summary>
public sealed class WeatherApiContractTests
{
    private static readonly Location Moscow = new("Москва", "Europe/Moscow", new GeoPoint(55.7522, 37.6156));

    [Fact]
    public async Task Successful_response_maps_to_the_application_model()
    {
        await using var host = WeatherApiTestHost.Create();
        StubSuccess(host.Server);

        WeatherSnapshot snapshot = await host.Provider.GetAsync(Moscow, 3, bypassCache: true, TestContext.Current.CancellationToken);

        snapshot.Location.City.Should().Be("Moscow");
        snapshot.Location.TimeZoneId.Should().Be("Europe/Moscow");
        snapshot.Location.Coordinates.Should().Be(new GeoPoint(55.7522, 37.6156));
        snapshot.Current.Temp.Celsius.Should().Be(22.3);
        snapshot.Current.FeelsLike.Celsius.Should().Be(23.1);
        snapshot.Current.Humidity.Should().Be(44);
        snapshot.Current.WindKph.Should().Be(9.4);
        snapshot.Current.PressureMb.Should().Be(1012);
        snapshot.Current.UvIndex.Should().Be(4.0);
        snapshot.Current.Condition.Text.Should().Be("Переменная облачность");
        snapshot.Days.Should().HaveCount(3);
        snapshot.Days[0].Hours.Should().HaveCount(24);
        snapshot.Days[0].Daily.Max.Celsius.Should().Be(24.0);
        snapshot.Days[0].Daily.Min.Celsius.Should().Be(12.0);
        snapshot.Days[0].Daily.ChanceOfRain.Should().Be(20);
        snapshot.LocalNow.Should().Be(new DateTimeOffset(2026, 8, 14, 15, 30, 0, TimeSpan.FromHours(3)));
    }

    [Fact]
    public async Task Protocol_relative_icon_is_normalised_to_https()
    {
        await using var host = WeatherApiTestHost.Create();
        StubSuccess(host.Server);

        WeatherSnapshot snapshot = await host.Provider.GetAsync(Moscow, 3, bypassCache: true, TestContext.Current.CancellationToken);

        snapshot.Current.Condition.IconUrl.Should().Be("https://cdn.weatherapi.com/weather/64x64/day/116.png");
    }

    [Fact]
    public async Task Request_uses_the_lat_lon_query_shape_required_by_the_task()
    {
        await using var host = WeatherApiTestHost.Create();
        StubSuccess(host.Server);

        await host.Provider.GetAsync(Moscow, 3, bypassCache: true, TestContext.Current.CancellationToken);

        string forecastQuery = QueryOf(host.Server, "/v1/forecast.json");

        forecastQuery.Should().Contain("q=55.7522%2C37.6156");
        forecastQuery.Should().Contain("days=3");
        forecastQuery.Should().Contain("lang=ru");
        forecastQuery.Should().Contain("aqi=no");
        forecastQuery.Should().Contain("alerts=no");
        QueryOf(host.Server, "/v1/current.json").Should().Contain("q=55.7522%2C37.6156");
    }

    [Fact]
    public async Task Both_endpoints_are_called_because_the_task_names_both()
    {
        await using var host = WeatherApiTestHost.Create();
        StubSuccess(host.Server);

        await host.Provider.GetAsync(Moscow, 3, bypassCache: true, TestContext.Current.CancellationToken);

        CallsTo(host.Server, "/v1/forecast.json").Should().Be(1);
        CallsTo(host.Server, "/v1/current.json").Should().Be(1);
    }

    [Fact]
    public async Task Forecast_only_mode_skips_the_second_endpoint()
    {
        await using var host = WeatherApiTestHost.Create(settings =>
            settings["WeatherApi:UseSeparateCurrentEndpoint"] = "false");
        StubSuccess(host.Server);

        WeatherSnapshot snapshot = await host.Provider.GetAsync(Moscow, 3, bypassCache: true, TestContext.Current.CancellationToken);

        CallsTo(host.Server, "/v1/current.json").Should().Be(0);
        snapshot.Current.Temp.Celsius.Should().Be(20.1, "forecast.json carries its own current block");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, WeatherFailureKind.Auth)]
    [InlineData(HttpStatusCode.Forbidden, WeatherFailureKind.Auth)]
    [InlineData(HttpStatusCode.TooManyRequests, WeatherFailureKind.RateLimit)]
    [InlineData(HttpStatusCode.InternalServerError, WeatherFailureKind.Provider)]
    [InlineData(HttpStatusCode.BadGateway, WeatherFailureKind.Provider)]
    public async Task Provider_status_codes_map_to_the_failure_taxonomy(HttpStatusCode status, WeatherFailureKind expected)
    {
        await using var host = WeatherApiTestHost.Create();
        Stub(host.Server, "/v1/forecast.json", status, WeatherApiFixtures.ProviderError);
        Stub(host.Server, "/v1/current.json", status, WeatherApiFixtures.ProviderError);

        Func<Task> act = () => host.Provider.GetAsync(Moscow, 3, bypassCache: true, TestContext.Current.CancellationToken);

        WeatherProviderException exception = (await act.Should().ThrowAsync<WeatherProviderException>()).Which;
        exception.Kind.Should().Be(expected);
    }

    /// <summary>
    /// Regression guard: the live provider sends pressure, humidity, and rain chance as decimals. Binding
    /// them as integers deserialised every hand-written fixture and failed against the real API.
    /// </summary>
    [Fact]
    public async Task Decimal_valued_integer_fields_deserialise()
    {
        await using var host = WeatherApiTestHost.Create(settings =>
            settings["WeatherApi:UseSeparateCurrentEndpoint"] = "false");
        Stub(host.Server, "/v1/forecast.json", HttpStatusCode.OK, WeatherApiFixtures.ForecastLiveShape);

        WeatherSnapshot snapshot = await host.Provider.GetAsync(Moscow, 1, bypassCache: true, TestContext.Current.CancellationToken);

        snapshot.Current.PressureMb.Should().Be(1013);
        snapshot.Current.Humidity.Should().Be(72);
        snapshot.Days[0].Daily.ChanceOfRain.Should().Be(85);
        snapshot.Days[0].Hours[0].ChanceOfRain.Should().Be(64);
    }

    [Fact]
    public async Task Malformed_payload_is_reported_as_a_protocol_failure()
    {
        await using var host = WeatherApiTestHost.Create();
        Stub(host.Server, "/v1/forecast.json", HttpStatusCode.OK, WeatherApiFixtures.Malformed);
        Stub(host.Server, "/v1/current.json", HttpStatusCode.OK, WeatherApiFixtures.Malformed);

        Func<Task> act = () => host.Provider.GetAsync(Moscow, 3, bypassCache: true, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WeatherProviderProtocolException>();
    }

    [Fact]
    public async Task An_unresponsive_provider_is_reported_as_a_timeout()
    {
        await using var host = WeatherApiTestHost.Create(settings =>
        {
            settings["WeatherApi:RequestTimeout"] = "00:00:01";
            settings["WeatherApi:TotalTimeout"] = "00:00:02";
        });
        StubDelayed(host.Server, "/v1/forecast.json", TimeSpan.FromSeconds(10));
        StubDelayed(host.Server, "/v1/current.json", TimeSpan.FromSeconds(10));

        Func<Task> act = () => host.Provider.GetAsync(Moscow, 3, bypassCache: true, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WeatherProviderTimeoutException>();
    }

    [Fact]
    public async Task Caller_cancellation_is_not_disguised_as_a_provider_timeout()
    {
        await using var host = WeatherApiTestHost.Create();
        StubDelayed(host.Server, "/v1/forecast.json", TimeSpan.FromSeconds(10));
        StubDelayed(host.Server, "/v1/current.json", TimeSpan.FromSeconds(10));
        using CancellationTokenSource source = new(TimeSpan.FromMilliseconds(200));

        Func<Task> act = () => host.Provider.GetAsync(Moscow, 3, bypassCache: true, source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Transient_server_errors_are_retried()
    {
        await using var host = WeatherApiTestHost.Create(settings =>
            settings["WeatherApi:MaxRetryAttempts"] = "2");
        StubFailThenSucceed(host.Server, "/v1/forecast.json", WeatherApiFixtures.ForecastSuccess);
        Stub(host.Server, "/v1/current.json", HttpStatusCode.OK, WeatherApiFixtures.CurrentSuccess);

        WeatherSnapshot snapshot = await host.Provider.GetAsync(Moscow, 3, bypassCache: true, TestContext.Current.CancellationToken);

        snapshot.Days.Should().HaveCount(3);
        CallsTo(host.Server, "/v1/forecast.json").Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task Rejected_credentials_are_not_retried()
    {
        await using var host = WeatherApiTestHost.Create(settings =>
            settings["WeatherApi:MaxRetryAttempts"] = "3");
        Stub(host.Server, "/v1/forecast.json", HttpStatusCode.Unauthorized, WeatherApiFixtures.ProviderError);
        Stub(host.Server, "/v1/current.json", HttpStatusCode.Unauthorized, WeatherApiFixtures.ProviderError);

        Func<Task> act = () => host.Provider.GetAsync(Moscow, 3, bypassCache: true, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WeatherProviderAuthException>();
        CallsTo(host.Server, "/v1/forecast.json").Should().Be(1, "a credential does not become valid on retry");
    }

    [Fact]
    public async Task Rate_limited_calls_are_not_retried_either()
    {
        await using var host = WeatherApiTestHost.Create(settings =>
            settings["WeatherApi:MaxRetryAttempts"] = "3");
        Stub(host.Server, "/v1/forecast.json", HttpStatusCode.TooManyRequests, WeatherApiFixtures.ProviderError);
        Stub(host.Server, "/v1/current.json", HttpStatusCode.TooManyRequests, WeatherApiFixtures.ProviderError);

        Func<Task> act = () => host.Provider.GetAsync(Moscow, 3, bypassCache: true, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WeatherProviderRateLimitException>();
        CallsTo(host.Server, "/v1/forecast.json").Should().Be(1, "retrying a rate limit only makes it worse");
    }

    [Fact]
    public async Task Partial_provider_data_does_not_break_the_mapping()
    {
        await using var host = WeatherApiTestHost.Create(settings =>
            settings["WeatherApi:UseSeparateCurrentEndpoint"] = "false");
        Stub(host.Server, "/v1/forecast.json", HttpStatusCode.OK, WeatherApiFixtures.ForecastPartial);

        WeatherSnapshot snapshot = await host.Provider.GetAsync(Moscow, 1, bypassCache: true, TestContext.Current.CancellationToken);

        snapshot.Days.Should().HaveCount(1);
        snapshot.Days[0].Hours.Should().HaveCount(2);
        snapshot.Days[0].Hours[0].Condition.Text.Should().Be("Нет данных");
        snapshot.Days[0].Hours[0].Condition.IconUrl.Should().BeNull();
        snapshot.Current.Condition.Text.Should().Be("Нет данных");
    }

    /// <summary>
    /// A blank credential must fail while the options graph is being built, not on the first user
    /// request: otherwise every visitor sees a generic error and the provider sees a burst of 401s.
    /// </summary>
    [Fact]
    public async Task Missing_credential_fails_fast_with_an_actionable_message()
    {
        await using var host = WeatherApiTestHost.Create(settings =>
            settings["WeatherApi:Credential"] = "   ");
        StubSuccess(host.Server);

        Func<object> act = () => host.Provider;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*Credential*");
        host.Server.LogEntries.Should().BeEmpty("no call should be attempted without a credential");
    }

    /// <summary>
    /// The credential travels in the query string, which makes accidental logging the single most likely
    /// way to leak it. This is the automated guard against that.
    /// </summary>
    [Fact]
    public async Task The_credential_never_reaches_the_logs()
    {
        await using var host = WeatherApiTestHost.Create();
        StubSuccess(host.Server);

        await host.Provider.GetAsync(Moscow, 3, bypassCache: true, TestContext.Current.CancellationToken);

        IReadOnlyList<FakeLogRecord> records = host.Logs.GetSnapshot();

        records.Should().NotBeEmpty("the client logs every provider call");
        records.Should().NotContain(
            record => record.Message.Contains(WeatherApiTestHost.Credential, StringComparison.Ordinal),
            "the WeatherAPI credential must never appear in a log message");
        records.Should().NotContain(
            record => record.Message.Contains("key=", StringComparison.Ordinal),
            "logging a query string would eventually leak the credential");
    }

    private static void StubSuccess(WireMockServer server)
    {
        Stub(server, "/v1/forecast.json", HttpStatusCode.OK, WeatherApiFixtures.ForecastSuccess);
        Stub(server, "/v1/current.json", HttpStatusCode.OK, WeatherApiFixtures.CurrentSuccess);
    }

    private static void Stub(WireMockServer server, string path, HttpStatusCode status, string body)
    {
        server
            .Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(status)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }

    private static void StubDelayed(WireMockServer server, string path, TimeSpan delay)
    {
        server
            .Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{}")
                .WithDelay(delay));
    }

    private static void StubFailThenSucceed(WireMockServer server, string path, string successBody)
    {
        server
            .Given(Request.Create().WithPath(path).UsingGet())
            .InScenario("retry")
            .WillSetStateTo("recovered")
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError).WithBody("{}"));

        server
            .Given(Request.Create().WithPath(path).UsingGet())
            .InScenario("retry")
            .WhenStateIs("recovered")
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(successBody));
    }

    private static int CallsTo(WireMockServer server, string path)
    {
        return server.LogEntries.Count(entry => MatchesPath(entry, path));
    }

    private static string QueryOf(WireMockServer server, string path)
    {
        ILogEntry entry = server.LogEntries.First(logEntry => MatchesPath(logEntry, path));

        return entry.RequestMessage?.RawQuery ?? string.Empty;
    }

    private static bool MatchesPath(ILogEntry entry, string path)
    {
        return string.Equals(entry.RequestMessage?.Path, path, StringComparison.Ordinal);
    }
}
