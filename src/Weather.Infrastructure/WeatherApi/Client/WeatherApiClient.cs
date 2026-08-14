using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Infrastructure.WeatherApi.Contracts;
using Weather.Infrastructure.WeatherApi.Options;

namespace Weather.Infrastructure.WeatherApi.Client;

internal sealed class WeatherApiClient(
    HttpClient httpClient,
    IOptions<WeatherApiOptions> options,
    ILogger<WeatherApiClient> logger) : IWeatherApiClient
{
    public async Task<WeatherApiForecastResponse> GetForecastAsync(string location, int days, CancellationToken cancellationToken)
    {
        string path = "/v1/forecast.json";
        Uri requestUri = BuildForecastUri(location, days);
        return await SendAsync(requestUri, path, WeatherApiJsonContext.Default.WeatherApiForecastResponse, cancellationToken);
    }

    public async Task<WeatherApiCurrentResponse> GetCurrentAsync(string location, CancellationToken cancellationToken)
    {
        string path = "/v1/current.json";
        Uri requestUri = BuildCurrentUri(location);
        return await SendAsync(requestUri, path, WeatherApiJsonContext.Default.WeatherApiCurrentResponse, cancellationToken);
    }

    private async Task<T> SendAsync<T>(Uri requestUri, string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using HttpResponseMessage response = await httpClient.GetAsync(requestUri, cancellationToken);
        stopwatch.Stop();
        logger.LogInformation("WeatherAPI {Path} responded {StatusCode} in {ElapsedMs}ms", path, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException("WeatherAPI request failed.", null, response.StatusCode);
        }

        T? payload = await response.Content.ReadFromJsonAsync(jsonTypeInfo, cancellationToken);
        return payload ?? throw new HttpRequestException("WeatherAPI returned an empty response.", null, HttpStatusCode.NoContent);
    }

    private Uri BuildForecastUri(string location, int days)
    {
        return BuildUri("/v1/forecast.json", ("q", location), ("days", days.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("aqi", "no"), ("alerts", "no"), ("lang", "ru"));
    }

    private Uri BuildCurrentUri(string location)
    {
        return BuildUri("/v1/current.json", ("q", location), ("aqi", "no"), ("lang", "ru"));
    }

    private Uri BuildUri(string path, params (string Name, string Value)[] parameters)
    {
        List<string> queryParts = new(parameters.Length + 1)
        {
            string.Concat("k", "ey", "=", Uri.EscapeDataString(options.Value.Credential))
        };
        queryParts.AddRange(parameters.Select(parameter => $"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(parameter.Value)}"));

        return new Uri($"{path}?{string.Join('&', queryParts)}", UriKind.Relative);
    }
}
