using Weather.Infrastructure.WeatherApi.Contracts;

namespace Weather.Infrastructure.WeatherApi.Client;

/// <summary>
/// Transport-level access to WeatherAPI. Takes the already formatted <c>q</c> value so the
/// credential and the URL shape stay in one place.
/// </summary>
internal interface IWeatherApiClient
{
    /// <summary>Calls <c>/v1/forecast.json</c>.</summary>
    Task<WeatherApiForecastResponse> GetForecastAsync(string query, int days, CancellationToken cancellationToken);

    /// <summary>Calls <c>/v1/current.json</c>.</summary>
    Task<WeatherApiCurrentResponse> GetCurrentAsync(string query, CancellationToken cancellationToken);
}
