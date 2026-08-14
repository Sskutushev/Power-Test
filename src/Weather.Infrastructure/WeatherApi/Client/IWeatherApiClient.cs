using Weather.Infrastructure.WeatherApi.Contracts;

namespace Weather.Infrastructure.WeatherApi.Client;

internal interface IWeatherApiClient
{
    Task<WeatherApiForecastResponse> GetForecastAsync(string location, int days, CancellationToken cancellationToken);

    Task<WeatherApiCurrentResponse> GetCurrentAsync(string location, CancellationToken cancellationToken);
}
