using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Scalar.AspNetCore;
using Weather.Application;
using Weather.Application.Weather;
using Weather.Infrastructure;
using Weather.Web.Diagnostics;
using Weather.Web.Endpoints;
using Weather.Web.Extensions;
using Weather.Web.HostedServices;

namespace Weather.Web;

/// <summary>
/// Host composition. Public so <c>WebApplicationFactory&lt;Program&gt;</c> can boot the real pipeline in tests.
/// </summary>
public sealed class Program
{
    private static readonly CultureInfo AppCulture = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>Entry point.</summary>
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        ConfigureServices(builder);

        WebApplication app = builder.Build();

        ConfigurePipeline(app);

        app.Run();
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        // MediatR's license notice is informational and would otherwise appear on every start.
        builder.Logging.AddFilter("LuckyPennySoftware.MediatR.License", LogLevel.None);

        builder.Services
            .AddOptions<WeatherOptions>()
            .Bind(builder.Configuration.GetSection("Weather"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddWeatherObservability(builder.Environment);

        ConfigureDataProtection(builder);

        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<WeatherExceptionHandler>();
        builder.Services.AddOpenApi();
        builder.Services.AddHealthChecks()
            .AddCheck<WeatherReadinessHealthCheck>("weather-configuration", tags: ["ready"]);
        builder.Services
            .AddOptions<ApiRateLimitOptions>()
            .Bind(builder.Configuration.GetSection("Api:RateLimit"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddRateLimiter(WeatherRateLimiting.Configure);

        // Registered unconditionally and gated inside the service: reading configuration here would
        // capture it before the host finishes composing its configuration sources.
        builder.Services.AddHostedService<WeatherRefreshBackgroundService>();
    }

    /// <summary>
    /// Persists data protection keys outside the image when a key path is supplied. This is what lets the
    /// container run with a read-only root filesystem: without it, key ring writes would fail on start.
    /// </summary>
    private static void ConfigureDataProtection(WebApplicationBuilder builder)
    {
        string? keyPath = builder.Configuration["Security:DataProtectionKeyPath"];

        if (string.IsNullOrWhiteSpace(keyPath))
        {
            return;
        }

        builder.Services
            .AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
            .SetApplicationName("weather-web");
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        CultureInfo.DefaultThreadCurrentCulture = AppCulture;
        CultureInfo.DefaultThreadCurrentUICulture = AppCulture;
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(AppCulture),
            SupportedCultures = [AppCulture],
            SupportedUICultures = [AppCulture]
        });

        // Registered once and unconditionally: the HTTP API must answer with ProblemDetails in every
        // environment, and a second registration would run the handler chain twice.
        app.UseExceptionHandler();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        // Only redirect when an HTTPS endpoint actually exists. In the container the app listens on
        // plain HTTP behind the ingress, and an unconditional redirect would break every request.
        if (HasHttpsEndpoint(app))
        {
            app.UseHttpsRedirection();
        }

        app.UseSecurityHeaders();
        app.UseStaticFiles();
        app.UseAntiforgery();
        app.UseRateLimiter();

        app.MapOpenApi();
        app.MapScalarApiReference("/docs", options => options
            .WithTitle("Weather API")
            .WithOpenApiRoutePattern("/openapi/{documentName}.json"));

        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false
        });
        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready")
        });

        app.MapWeatherEndpoints();
        app.MapRazorComponents<Components.App>().AddInteractiveServerRenderMode();
    }

    private static bool HasHttpsEndpoint(WebApplication app)
    {
        if (!string.IsNullOrWhiteSpace(app.Configuration["HTTPS_PORT"])
            || !string.IsNullOrWhiteSpace(app.Configuration["ASPNETCORE_HTTPS_PORTS"]))
        {
            return true;
        }

        string? urls = app.Configuration["ASPNETCORE_URLS"] ?? app.Configuration["urls"];

        return urls?.Contains("https://", StringComparison.OrdinalIgnoreCase) == true;
    }
}
