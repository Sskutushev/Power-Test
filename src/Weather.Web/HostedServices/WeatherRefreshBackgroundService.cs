using MediatR;
using Microsoft.Extensions.Options;
using Weather.Application.Common;
using Weather.Application.Weather;
using Weather.Application.Weather.GetRegionalWeather;
using Weather.Application.Weather.GetWeatherDashboard;

namespace Weather.Web.HostedServices;

/// <summary>
/// Optional scheduled cache refresh, disabled by default. It exists to show scheduled work done with the
/// platform's own primitives; a job framework would be unjustified for a single periodic cache warm-up.
/// </summary>
public sealed class WeatherRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<WeatherOptions> options,
    ILogger<WeatherRefreshBackgroundService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WeatherOptions weatherOptions = options.Value;

        if (!weatherOptions.BackgroundRefresh.Enabled)
        {
            logger.LogInformation("weather_refresh_disabled");
            return;
        }

        using PeriodicTimer timer = new(weatherOptions.BackgroundRefresh.Interval);

        logger.LogInformation(
            "weather_refresh_started {Interval}",
            weatherOptions.BackgroundRefresh.Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshAsync(weatherOptions.Region.Enabled, stoppingToken);
        }
    }

    private async Task RefreshAsync(bool includeRegion, CancellationToken stoppingToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            await sender.Send(new GetWeatherDashboardQuery(BypassCache: true), stoppingToken);

            if (includeRegion)
            {
                await sender.Send(new GetRegionalWeatherQuery(BypassCache: true), stoppingToken);
            }

            WeatherTelemetry.RefreshExecutions.Add(1, new KeyValuePair<string, object?>("outcome", "success"));
            logger.LogInformation("weather_refresh_completed");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A provider outage must never take the host down with it.
            WeatherTelemetry.RefreshExecutions.Add(1, new KeyValuePair<string, object?>("outcome", "failure"));
            logger.LogWarning(exception, "weather_refresh_failed");
        }
    }
}
