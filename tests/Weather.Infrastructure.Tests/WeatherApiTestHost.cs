using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Weather.Application.Abstractions;
using Weather.Infrastructure.WeatherApi;
using WireMock.Server;

namespace Weather.Infrastructure.Tests;

/// <summary>
/// Boots the real Infrastructure composition root against a local stub server. Contract tests therefore
/// exercise the production typed client, resilience pipeline, and mapping — only the network peer is faked.
/// </summary>
internal sealed class WeatherApiTestHost : IAsyncDisposable
{
    public const string Credential = "test-credential-2f8d41";

    private readonly ServiceProvider services;

    private WeatherApiTestHost(WireMockServer server, ServiceProvider services)
    {
        Server = server;
        this.services = services;
    }

    public WireMockServer Server { get; }

    /// <summary>Raw adapter without the caching decorator, so each test controls its own call count.</summary>
    public WeatherApiProvider Provider => services.GetRequiredService<WeatherApiProvider>();

    public WeatherApiRegionalProvider RegionalProvider => services.GetRequiredService<WeatherApiRegionalProvider>();

    public IWeatherProvider CachingProvider => services.GetRequiredService<IWeatherProvider>();

    public FakeLogCollector Logs => services.GetRequiredService<FakeLogCollector>();

    public static WeatherApiTestHost Create(Action<Dictionary<string, string?>>? configure = null)
    {
        var server = WireMockServer.Start();

        Dictionary<string, string?> settings = new(StringComparer.Ordinal)
        {
            ["WeatherApi:BaseUrl"] = server.Url,
            ["WeatherApi:Credential"] = Credential,
            // Generous on purpose: seven test projects run in parallel and a saturated machine can make a
            // local stub answer slowly. Timeout behaviour itself is covered by a test that sets its own
            // short budget, so a tight default here would only buy flakes.
            ["WeatherApi:RequestTimeout"] = "00:00:20",
            ["WeatherApi:TotalTimeout"] = "00:00:40",
            ["WeatherApi:MaxRetryAttempts"] = "0",
            ["WeatherApi:UseSeparateCurrentEndpoint"] = "true",
            ["Weather:Cache:LocalCacheExpiration"] = "00:05:00",
            ["Weather:Cache:Expiration"] = "00:10:00",
            ["Weather:Cache:RegionExpiration"] = "00:15:00",
            ["Weather:Cache:StaleExpiration"] = "01:00:00"
        };
        configure?.Invoke(settings);

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        ServiceCollection collection = [];
        collection.AddSingleton(TimeProvider.System);
        collection.AddLogging(builder => builder.AddFakeLogging().SetMinimumLevel(LogLevel.Trace));
        collection.AddInfrastructure(configuration);

        return new WeatherApiTestHost(server, collection.BuildServiceProvider());
    }

    public async ValueTask DisposeAsync()
    {
        await services.DisposeAsync();
        Server.Dispose();
    }
}
