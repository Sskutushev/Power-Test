using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Application.Common;
using Weather.Infrastructure.WeatherApi.Contracts;
using Weather.Infrastructure.WeatherApi.Options;

namespace Weather.Infrastructure.WeatherApi.Client;

/// <summary>
/// Typed HttpClient for WeatherAPI. The credential travels in the query string, so this type never
/// logs a request URI: only the path, the status code, and the elapsed time.
/// </summary>
internal sealed class WeatherApiClient(
    HttpClient httpClient,
    IOptions<WeatherApiOptions> options,
    ILogger<WeatherApiClient> logger) : IWeatherApiClient
{
    private const string ForecastPath = "/v1/forecast.json";
    private const string CurrentPath = "/v1/current.json";

    /// <inheritdoc />
    public Task<WeatherApiForecastResponse> GetForecastAsync(string query, int days, CancellationToken cancellationToken)
    {
        Uri requestUri = BuildUri(
            ForecastPath,
            ("q", query),
            ("days", days.ToString(CultureInfo.InvariantCulture)),
            ("aqi", "no"),
            ("alerts", "no"),
            ("lang", "ru"));

        return SendAsync(requestUri, ForecastPath, WeatherApiJsonContext.Default.WeatherApiForecastResponse, cancellationToken);
    }

    /// <inheritdoc />
    public Task<WeatherApiCurrentResponse> GetCurrentAsync(string query, CancellationToken cancellationToken)
    {
        Uri requestUri = BuildUri(CurrentPath, ("q", query), ("aqi", "no"), ("lang", "ru"));

        return SendAsync(requestUri, CurrentPath, WeatherApiJsonContext.Default.WeatherApiCurrentResponse, cancellationToken);
    }

    private async Task<T> SendAsync<T>(
        Uri requestUri,
        string path,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken)
    {
        long startTimestamp = Stopwatch.GetTimestamp();
        using HttpResponseMessage response = await httpClient.GetAsync(requestUri, cancellationToken);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

        WeatherTelemetry.ProviderDuration.Record(
            elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("path", path),
            new KeyValuePair<string, object?>("status", (int)response.StatusCode));
        logger.LogInformation(
            "weather_provider_call {Path} responded {StatusCode} in {ElapsedMs}ms",
            path,
            (int)response.StatusCode,
            (long)elapsed.TotalMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException("WeatherAPI request failed.", null, response.StatusCode);
        }

        T? payload = await response.Content.ReadFromJsonAsync(jsonTypeInfo, cancellationToken);

        return payload ?? throw new HttpRequestException("WeatherAPI returned an empty response.", null, HttpStatusCode.NoContent);
    }

    private Uri BuildUri(string path, params ReadOnlySpan<(string Name, string Value)> parameters)
    {
        List<string> queryParts = new(parameters.Length + 1)
        {
            $"key={Uri.EscapeDataString(options.Value.Credential)}"
        };

        foreach ((string name, string value) in parameters)
        {
            queryParts.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
        }

        return new Uri($"{path}?{string.Join('&', queryParts)}", UriKind.Relative);
    }
}
