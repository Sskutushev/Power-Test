using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using WireMock.Server;

namespace Weather.IntegrationTests;

/// <summary>
/// Boots the real application in memory. Only the outbound WeatherAPI peer is replaced — DI, options
/// validation, MediatR, caching, resilience, rate limiting, and the endpoints are the production ones.
/// </summary>
internal sealed class WeatherApplicationFactory : WebApplicationFactory<Weather.Web.Program>
{
    private readonly Dictionary<string, string?> settings;

    public WeatherApplicationFactory(WireMockServer server, Action<Dictionary<string, string?>>? configure = null)
    {
        settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["WeatherApi:BaseUrl"] = server.Url,
            ["WeatherApi:Credential"] = "test-credential-9a1c",
            ["WeatherApi:UseSeparateCurrentEndpoint"] = "true",
            ["WeatherApi:MaxRetryAttempts"] = "0",
            ["WeatherApi:RequestTimeout"] = "00:00:05",
            ["WeatherApi:TotalTimeout"] = "00:00:10",
            ["Weather:Location"] = "Москва",
            ["Weather:Latitude"] = "55.7522",
            ["Weather:Longitude"] = "37.6156",
            ["Weather:ForecastDays"] = "3",
            ["Weather:Cache:LocalCacheExpiration"] = "00:05:00",
            ["Weather:Cache:Expiration"] = "00:05:00",
            ["Weather:Cache:RegionExpiration"] = "00:05:00",
            ["Weather:Cache:StaleExpiration"] = "01:00:00",
            // Region points are deliberately not overridden: configuration arrays merge rather than
            // replace, and the shipped point list is worth exercising as-is.
            ["Weather:Region:Enabled"] = "true",
            ["Api:RateLimit:PermitLimit"] = "30",
            ["Api:RateLimit:Window"] = "00:01:00"
        };

        configure?.Invoke(settings);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
    }
}
