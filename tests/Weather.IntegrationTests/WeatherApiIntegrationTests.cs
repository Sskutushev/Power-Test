using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Weather.Application.Weather.GetRegionalWeather;
using Weather.Application.Weather.GetWeatherDashboard;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Weather.IntegrationTests;

/// <summary>
/// End-to-end through the real host: routing, MediatR, caching, resilience, problem details, rate limiting.
/// </summary>
public sealed class WeatherApiIntegrationTests : IDisposable
{
    private readonly WireMockServer server = WireMockServer.Start();

    [Fact]
    public async Task Application_starts_with_a_complete_dependency_graph()
    {
        await using WeatherApplicationFactory factory = new(server);

        HttpClient client = factory.CreateClient();

        client.Should().NotBeNull("resolving the host proves every registration can be constructed");
    }

    [Fact]
    public async Task Missing_credential_stops_the_application_at_startup()
    {
        await using WeatherApplicationFactory factory = new(server, settings => settings["WeatherApi:Credential"] = string.Empty);

        Exception? failure = Record.Exception(() => factory.CreateClient());

        failure.Should().NotBeNull();
        failure!.ToString().Should().Contain("Credential", "a misconfiguration must fail fast, not per request");
    }

    [Fact]
    public async Task Dashboard_endpoint_returns_the_filtered_forecast()
    {
        StubSuccess();
        await using WeatherApplicationFactory factory = new(server);
        HttpClient client = factory.CreateClient();

        WeatherDashboardDto? result = await client.GetFromJsonAsync<WeatherDashboardDto>("/api/weather", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Location.City.Should().Be("Moscow");

        // Provider local time is 15:30, so today contributes hours 15..23 and tomorrow all 24.
        result.Hourly.Should().HaveCount(33);
        result.Daily.Should().HaveCount(3);
        result.IsStale.Should().BeFalse();
    }

    [Fact]
    public async Task Region_endpoint_returns_one_marker_per_configured_point()
    {
        StubSuccess();
        await using WeatherApplicationFactory factory = new(server);
        HttpClient client = factory.CreateClient();

        RegionalWeatherDto? result = await client.GetFromJsonAsync<RegionalWeatherDto>("/api/weather/region", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Points.Should().HaveCount(9, "the shipped configuration samples nine territory points");
        result.Points.Select(point => point.Name).Should().Contain(["Москва", "Тверь", "Нижний Новгород"]);
        result.Points.Should().OnlyContain(point => point.TempC != 0);
        result.CenterLatitude.Should().Be(55.7522);
        result.Zoom.Should().Be(6);
    }

    [Fact]
    public async Task Disabling_the_map_stops_its_provider_calls_entirely()
    {
        StubSuccess();
        await using WeatherApplicationFactory factory = new(server, settings => settings["Weather:Region:Enabled"] = "false");
        HttpClient client = factory.CreateClient();

        RegionalWeatherDto? result = await client.GetFromJsonAsync<RegionalWeatherDto>("/api/weather/region", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Points.Should().BeEmpty();
        server.LogEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task Provider_outage_becomes_a_service_unavailable_problem_document()
    {
        StubStatus(HttpStatusCode.InternalServerError);
        await using WeatherApplicationFactory factory = new(server);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/weather", TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        body.Should().Contain("traceId");
        body.Should().NotContainAny("Exception", "   at ", "Credential", "key=");
    }

    [Fact]
    public async Task Rejected_credentials_never_hint_at_the_credential_in_the_response()
    {
        StubStatus(HttpStatusCode.Unauthorized);
        await using WeatherApplicationFactory factory = new(server);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/weather", TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().NotContainAny("Credential", "credential", "Unauthorized", "api key", "API key");
    }

    [Fact]
    public async Task Upstream_rate_limiting_is_reported_with_a_retry_hint()
    {
        StubStatus(HttpStatusCode.TooManyRequests);
        await using WeatherApplicationFactory factory = new(server);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/weather", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task Malformed_upstream_payloads_are_reported_as_a_bad_gateway()
    {
        StubBody("{ \"location\": ", "{ \"location\": ");
        await using WeatherApplicationFactory factory = new(server);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/weather", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task A_second_request_within_the_cache_window_does_not_reach_the_provider()
    {
        StubSuccess();
        await using WeatherApplicationFactory factory = new(server);
        HttpClient client = factory.CreateClient();

        await client.GetAsync("/api/weather", TestContext.Current.CancellationToken);
        int afterFirst = ForecastCalls();
        await client.GetAsync("/api/weather", TestContext.Current.CancellationToken);

        afterFirst.Should().Be(1);
        ForecastCalls().Should().Be(1, "the second read must be served from cache");
    }

    /// <summary>
    /// Stampede protection: a burst of cold-cache readers has to collapse into a single upstream call,
    /// otherwise a restart under load would multiply traffic against a metered provider.
    /// </summary>
    [Fact]
    public async Task A_burst_of_concurrent_readers_produces_one_upstream_call()
    {
        StubSuccess();
        await using WeatherApplicationFactory factory = new(server, settings => settings["Api:RateLimit:PermitLimit"] = "500");
        HttpClient client = factory.CreateClient();

        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, 50).Select(_ => client.GetAsync("/api/weather", TestContext.Current.CancellationToken)));

        responses.Should().AllSatisfy(response => response.StatusCode.Should().Be(HttpStatusCode.OK));
        ForecastCalls().Should().Be(1);

        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Health_probes_answer_without_calling_the_provider()
    {
        StubSuccess();
        await using WeatherApplicationFactory factory = new(server);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        HttpResponseMessage ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        live.StatusCode.Should().Be(HttpStatusCode.OK);
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        server.LogEntries.Should().BeEmpty("readiness must not depend on a third party's uptime or quota");
    }

    [Fact]
    public async Task The_public_endpoint_is_rate_limited()
    {
        StubSuccess();
        await using WeatherApplicationFactory factory = new(server);
        HttpClient client = factory.CreateClient();

        HttpStatusCode last = HttpStatusCode.OK;
        for (int request = 0; request <= 31 && last != HttpStatusCode.TooManyRequests; request++)
        {
            using HttpResponseMessage response = await client.GetAsync("/api/weather", TestContext.Current.CancellationToken);
            last = response.StatusCode;
        }

        last.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Responses_carry_baseline_security_headers()
    {
        StubSuccess();
        await using WeatherApplicationFactory factory = new(server);
        HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Contain("strict-origin-when-cross-origin");
        string csp = string.Join(", ", response.Headers.GetValues("Content-Security-Policy"));
        csp.Should().Contain("script-src 'self'");
        csp.Should().Contain("frame-ancestors 'none'");
        csp.Should().Contain("object-src 'none'");
    }

    [Fact]
    public async Task The_openapi_document_is_published()
    {
        StubSuccess();
        await using WeatherApplicationFactory factory = new(server);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        string document = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.Should().Contain("/api/weather");
        document.Should().NotContain("Credential");
    }

    /// <summary>
    /// Graceful degradation: once a good snapshot exists, a provider outage should downgrade the screen
    /// to stale data rather than to an error.
    /// </summary>
    [Fact]
    public async Task An_outage_after_a_successful_read_is_served_from_the_stale_copy()
    {
        StubSuccess();
        await using WeatherApplicationFactory factory = new(server, settings =>
        {
            settings["Weather:Cache:Expiration"] = "00:00:01";
            settings["Weather:Cache:LocalCacheExpiration"] = "00:00:01";
        });
        HttpClient client = factory.CreateClient();

        await client.GetAsync("/api/weather", TestContext.Current.CancellationToken);

        server.Reset();
        StubStatus(HttpStatusCode.InternalServerError);
        await Task.Delay(TimeSpan.FromMilliseconds(1300), TestContext.Current.CancellationToken);

        WeatherDashboardDto? stale = await client.GetFromJsonAsync<WeatherDashboardDto>("/api/weather", TestContext.Current.CancellationToken);

        stale.Should().NotBeNull();
        stale!.IsStale.Should().BeTrue();
        stale.Daily.Should().HaveCount(3);
    }

    public void Dispose()
    {
        server.Dispose();
    }

    private int ForecastCalls()
    {
        return server.LogEntries.Count(entry =>
            string.Equals(entry.RequestMessage?.Path, "/v1/forecast.json", StringComparison.Ordinal));
    }

    private void StubSuccess()
    {
        StubBody(IntegrationFixtures.Forecast, IntegrationFixtures.Current);
    }

    private void StubStatus(HttpStatusCode status)
    {
        Stub("/v1/forecast.json", status, "{}");
        Stub("/v1/current.json", status, "{}");
    }

    private void StubBody(string forecastBody, string currentBody)
    {
        Stub("/v1/forecast.json", HttpStatusCode.OK, forecastBody);
        Stub("/v1/current.json", HttpStatusCode.OK, currentBody);
    }

    private void Stub(string path, HttpStatusCode status, string body)
    {
        server
            .Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(status)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }
}
