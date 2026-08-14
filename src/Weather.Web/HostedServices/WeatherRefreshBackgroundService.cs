using MediatR;
using Microsoft.Extensions.Options;
using Weather.Application.Common;
using Weather.Application.Weather;
using Weather.Application.Weather.GetWeatherDashboard;

namespace Weather.Web.HostedServices;

public sealed class WeatherRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<WeatherOptions> options,
    ILogger<WeatherRefreshBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = options.Value.BackgroundRefresh.Interval;
        using PeriodicTimer timer = new(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshAsync(stoppingToken);
        }
    }

    private async Task RefreshAsync(CancellationToken stoppingToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new GetWeatherDashboardQuery(BypassCache: true), stoppingToken);
            WeatherTelemetry.RefreshExecutions.Add(1, new KeyValuePair<string, object?>("outcome", "success"));
            logger.LogInformation("weather_refresh_completed");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            WeatherTelemetry.RefreshExecutions.Add(1, new KeyValuePair<string, object?>("outcome", "failure"));
            logger.LogWarning(exception, "weather_refresh_failed");
        }
    }
}
