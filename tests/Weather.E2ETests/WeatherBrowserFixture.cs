using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Weather.E2ETests;

/// <summary>
/// Runs the real application on a real Kestrel port with a stubbed WeatherAPI, and drives it with a real
/// browser. Nothing here reaches the internet: the provider is local and the map assets are vendored.
/// </summary>
public sealed class WeatherBrowserFixture : WebApplicationFactory<Weather.Web.Program>, IAsyncLifetime
{
    private readonly WireMockServer provider = WireMockServer.Start();
    private IHost? kestrelHost;
    private IPlaywright? playwright;

    public string ServerAddress { get; private set; } = string.Empty;

    public IBrowser? Browser { get; private set; }

    /// <summary>Null when no Playwright browser is provisioned on this machine.</summary>
    public string? UnavailableReason { get; private set; }

    public async ValueTask InitializeAsync()
    {

        // Materialises the Kestrel host as a side effect of the base class creating its test host.
        using (HttpClient warmup = CreateClient())
        {
            await warmup.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);
        }

        await StubProviderAsync(success: true);

        try
        {
            playwright = await Playwright.CreateAsync();
            Browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }
        catch (PlaywrightException exception)
        {
            // The browser binary is provisioned by a dedicated CI step; a developer machine without it
            // should report the E2E suite as skipped rather than as broken.
            UnavailableReason = $"Playwright browser is not installed: {exception.Message}";
        }
    }

    public async Task<IPage> NewPageAsync()
    {
        IBrowserContext context = await Browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            Locale = "ru-RU",
            ReducedMotion = ReducedMotion.Reduce
        });

        return await context.NewPageAsync();
    }

    /// <summary>
    /// Points the stub at a healthy or broken provider and clears the weather cache, so each scenario
    /// starts from a cold cache instead of inheriting the previous test's snapshot or stale copy.
    /// </summary>
    public async Task StubProviderAsync(bool success)
    {
        provider.Reset();

        int status = success ? 200 : 500;
        Respond("/v1/forecast.json", status, success ? E2EFixtures.Forecast : "{}");
        Respond("/v1/current.json", status, success ? E2EFixtures.Current : "{}");

        await ResetCacheAsync();
    }

    private async Task ResetCacheAsync()
    {
        if (kestrelHost is null)
        {
            return;
        }

        HybridCache cache = kestrelHost.Services.GetRequiredService<HybridCache>();
        await cache.RemoveByTagAsync("weather");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["WeatherApi:BaseUrl"] = provider.Url,
                ["WeatherApi:Credential"] = "e2e-credential",
                ["WeatherApi:MaxRetryAttempts"] = "0",
                ["WeatherApi:RequestTimeout"] = "00:00:20",
                ["WeatherApi:TotalTimeout"] = "00:00:40",
                // The breaker exists for production; inside a browser suite that flips the provider
                // between healthy and broken it would only make results depend on test order.
                ["WeatherApi:CircuitBreaker:MinimumThroughput"] = "100000",
                ["Weather:Cache:Expiration"] = "00:00:01",
                ["Weather:Cache:LocalCacheExpiration"] = "00:00:01",
                ["Weather:Cache:StaleExpiration"] = "00:00:01",
                ["Weather:Region:Enabled"] = "false",
                ["Api:RateLimit:PermitLimit"] = "1000"
            }));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // The base class needs its in-memory host; the browser needs a socket. Build both from the same
        // configuration so the two views of the app can never drift apart.
        IHost testHost = builder.Build();

        builder.ConfigureWebHost(webHost => webHost.UseKestrel());
        kestrelHost = builder.Build();
        kestrelHost.Start();

        IServer server = kestrelHost.Services.GetRequiredService<IServer>();
        ServerAddress = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();

        testHost.Start();

        return testHost;
    }

    public override async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        playwright?.Dispose();

        if (kestrelHost is not null)
        {
            await kestrelHost.StopAsync();
            kestrelHost.Dispose();
        }

        provider.Dispose();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private void Respond(string path, int status, string body)
    {
        provider
            .Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(status)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }
}
