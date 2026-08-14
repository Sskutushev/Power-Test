using System.Threading.RateLimiting;
using Weather.Application;
using Weather.Application.Weather;
using Weather.Infrastructure;
using Weather.Web.Endpoints;
using Weather.Web.Extensions;
using Weather.Web.HostedServices;

namespace Weather.Web;

public sealed class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Logging.AddFilter("LuckyPennySoftware.MediatR.License", LogLevel.None);
        builder.Services.Configure<WeatherOptions>(builder.Configuration.GetSection("Weather"));
        WeatherOptions weatherOptions = builder.Configuration.GetSection("Weather").Get<WeatherOptions>() ?? new WeatherOptions();
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<WeatherExceptionHandler>();
        builder.Services.AddOpenApi();
        builder.Services.AddHealthChecks();
        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy("weather-api", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });
        if (weatherOptions.BackgroundRefresh.Enabled)
        {
            builder.Services.AddHostedService<WeatherRefreshBackgroundService>();
        }

        WebApplication app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler();
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseAntiforgery();
        app.UseExceptionHandler();
        app.UseRateLimiter();

        app.MapOpenApi();
        app.MapHealthChecks("/health/live");
        app.MapHealthChecks("/health/ready");
        app.MapWeatherEndpoints();
        app.MapRazorComponents<Components.App>().AddInteractiveServerRenderMode();

        app.Run();
    }
}
