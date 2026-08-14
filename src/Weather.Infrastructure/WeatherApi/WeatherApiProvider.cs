using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Application.Abstractions;
using Weather.Application.Common;
using Weather.Domain;
using Weather.Infrastructure.WeatherApi.Client;
using Weather.Infrastructure.WeatherApi.Mapping;
using Weather.Infrastructure.WeatherApi.Options;

namespace Weather.Infrastructure.WeatherApi;

internal sealed class WeatherApiProvider(
    IWeatherApiClient client,
    IOptions<WeatherApiOptions> options) : IWeatherProvider
{
    public async Task<WeatherSnapshot> GetAsync(Location location, int forecastDays, bool bypassCache, CancellationToken cancellationToken)
    {
        try
        {
            Task<Contracts.WeatherApiForecastResponse> forecastTask = client.GetForecastAsync(location.City, forecastDays, cancellationToken);

            if (!options.Value.UseSeparateCurrentEndpoint)
            {
                Contracts.WeatherApiForecastResponse forecastOnly = await forecastTask;
                return WeatherApiMapper.Map(forecastOnly, null);
            }

            Task<Contracts.WeatherApiCurrentResponse> currentTask = client.GetCurrentAsync(location.City, cancellationToken);
            await Task.WhenAll(forecastTask, currentTask);

            return WeatherApiMapper.Map(await forecastTask, await currentTask);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException exception)
        {
            throw new WeatherProviderTimeoutException("WeatherAPI request timed out.", exception);
        }
        catch (JsonException exception)
        {
            throw new WeatherProviderProtocolException("WeatherAPI response could not be parsed.", exception);
        }
        catch (HttpRequestException exception) when (exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new WeatherProviderAuthException("WeatherAPI credentials were rejected.");
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new WeatherProviderRateLimitException("WeatherAPI rate limit was exceeded.");
        }
        catch (HttpRequestException exception)
        {
            throw new WeatherProviderException(WeatherFailureKind.Provider, "WeatherAPI request failed.", exception);
        }
    }
}
