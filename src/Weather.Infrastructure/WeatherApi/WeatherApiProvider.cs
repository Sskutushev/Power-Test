using System.Diagnostics;
using Microsoft.Extensions.Options;
using Weather.Application.Abstractions;
using Weather.Application.Common;
using Weather.Domain;
using Weather.Infrastructure.WeatherApi.Client;
using Weather.Infrastructure.WeatherApi.Contracts;
using Weather.Infrastructure.WeatherApi.Mapping;
using Weather.Infrastructure.WeatherApi.Options;

namespace Weather.Infrastructure.WeatherApi;

/// <summary>
/// Adapter from WeatherAPI to the Application provider contract.
/// </summary>
internal sealed class WeatherApiProvider(
    IWeatherApiClient client,
    IOptions<WeatherApiOptions> options) : IWeatherProvider
{
    /// <inheritdoc />
    public async Task<WeatherSnapshot> GetAsync(Location location, int forecastDays, bool bypassCache, CancellationToken cancellationToken)
    {
        WeatherApiOptions apiOptions = options.Value;

        using Activity? activity = WeatherTelemetry.ActivitySource.StartActivity("weather.provider.dashboard");
        string query = location.Coordinates.ToQueryValue();

        try
        {
            Task<WeatherApiForecastResponse> forecastTask = client.GetForecastAsync(query, forecastDays, cancellationToken);

            if (!apiOptions.UseSeparateCurrentEndpoint)
            {
                return WeatherApiMapper.Map(await forecastTask, null);
            }

            // The ТЗ names both endpoints, so both are called; running them concurrently keeps the
            // extra call off the critical path. See ADR-002 for why forecast.json alone would suffice.
            Task<WeatherApiCurrentResponse> currentTask = client.GetCurrentAsync(query, cancellationToken);
            await Task.WhenAll(forecastTask, currentTask).ConfigureAwait(false);

            return WeatherApiMapper.Map(await forecastTask, await currentTask);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            throw ProviderFailureMapper.Map(exception);
        }
    }
}
