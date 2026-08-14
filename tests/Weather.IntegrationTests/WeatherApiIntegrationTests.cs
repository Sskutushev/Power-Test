using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Weather.Application.Weather.GetWeatherDashboard;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Weather.IntegrationTests;

public sealed class WeatherApiIntegrationTests : IDisposable
{
    private readonly WireMockServer server = WireMockServer.Start();

    [Fact]
    public async Task Api_weather_returns_dashboard_from_real_pipeline()
    {
        ConfigureWeatherApi(HttpStatusCode.OK, ForecastJson, CurrentJson);
        await using WebApplicationFactory<Weather.Web.Program> factory = CreateFactory();
        HttpClient client = factory.CreateClient();

        WeatherDashboardDto? result = await client.GetFromJsonAsync<WeatherDashboardDto>("/api/weather", Xunit.TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Location.City.Should().Be("Moscow");
        result.Hourly.Should().HaveCount(33);
        result.Daily.Should().HaveCount(3);
    }

    [Fact]
    public async Task Api_weather_maps_provider_failure_to_problem_details()
    {
        ConfigureWeatherApi(HttpStatusCode.InternalServerError, "{}", "{}");
        await using WebApplicationFactory<Weather.Web.Program> factory = CreateFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/weather", Xunit.TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        body.Should().Contain("traceId");
        body.Should().NotContain("Credential");
    }

    [Fact]
    public async Task Health_endpoints_do_not_call_weather_provider()
    {
        await using WebApplicationFactory<Weather.Web.Program> factory = CreateFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage live = await client.GetAsync("/health/live", Xunit.TestContext.Current.CancellationToken);
        HttpResponseMessage ready = await client.GetAsync("/health/ready", Xunit.TestContext.Current.CancellationToken);

        live.StatusCode.Should().Be(HttpStatusCode.OK);
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        server.LogEntries.Should().BeEmpty();
    }

    public void Dispose()
    {
        server.Dispose();
    }

    private WebApplicationFactory<Weather.Web.Program> CreateFactory()
    {
        return new WebApplicationFactory<Weather.Web.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["WeatherApi:BaseUrl"] = server.Url,
                        ["WeatherApi:Credential"] = "test-credential",
                        ["WeatherApi:UseSeparateCurrentEndpoint"] = "true",
                        ["Weather:Cache:LocalCacheExpiration"] = "00:00:01",
                        ["Weather:Cache:Expiration"] = "00:00:01"
                    });
                });
            });
    }

    private void ConfigureWeatherApi(HttpStatusCode statusCode, string forecastBody, string currentBody)
    {
        server.Given(Request.Create().WithPath("/v1/forecast.json").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(statusCode).WithBody(forecastBody).WithHeader("Content-Type", "application/json"));
        server.Given(Request.Create().WithPath("/v1/current.json").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(statusCode).WithBody(currentBody).WithHeader("Content-Type", "application/json"));
    }

    private const string CurrentJson = """
        {
          "location": { "name": "Moscow", "tz_id": "Europe/Moscow", "localtime": "2026-08-14 15:30", "localtime_epoch": 1786710600 },
          "current": {
            "last_updated": "2026-08-14 15:15",
            "last_updated_epoch": 1786709700,
            "temp_c": 22.3,
            "feelslike_c": 23.1,
            "humidity": 44,
            "wind_kph": 9.4,
            "pressure_mb": 1012,
            "uv": 4.0,
            "condition": { "text": "Переменная облачность", "icon": "//cdn.weatherapi.com/weather/64x64/day/116.png", "code": 1003 }
          }
        }
        """;

    private static readonly string ForecastJson = $$"""
        {
          "location": { "name": "Moscow", "tz_id": "Europe/Moscow", "localtime": "2026-08-14 15:30", "localtime_epoch": 1786710600 },
          "current": {
            "last_updated": "2026-08-14 15:15",
            "last_updated_epoch": 1786709700,
            "temp_c": 22.3,
            "feelslike_c": 23.1,
            "humidity": 44,
            "wind_kph": 9.4,
            "pressure_mb": 1012,
            "uv": 4.0,
            "condition": { "text": "Переменная облачность", "icon": "//cdn.weatherapi.com/weather/64x64/day/116.png", "code": 1003 }
          },
          "forecast": {
            "forecastday": [
              {{BuildDay("2026-08-14")}},
              {{BuildDay("2026-08-15")}},
              {{BuildDay("2026-08-16")}}
            ]
          }
        }
        """;

    private static string BuildDay(string date)
    {
        string hours = string.Join(
            ',',
            Enumerable.Range(0, 24).Select(hour => $$"""
                {
                  "time": "{{date}} {{hour:00}}:00",
                  "temp_c": {{10 + hour}},
                  "wind_kph": 5.0,
                  "chance_of_rain": 10,
                  "condition": { "text": "Ясно", "icon": "", "code": 1000 }
                }
                """));

        return $$"""
            {
              "date": "{{date}}",
              "day": {
                "maxtemp_c": 24.0,
                "mintemp_c": 12.0,
                "daily_chance_of_rain": 20,
                "condition": { "text": "Ясно", "icon": "//cdn.weatherapi.com/weather/64x64/day/113.png", "code": 1000 }
              },
              "hour": [{{hours}}]
            }
            """;
    }
}
