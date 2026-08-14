using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Weather.Application.Weather;
using Weather.Infrastructure.WeatherApi.Options;

namespace Weather.Web.Diagnostics;

/// <summary>
/// Readiness probe. It validates that the application is configured well enough to serve traffic and
/// deliberately does <b>not</b> call WeatherAPI: an orchestrator polls readiness every few seconds, so a
/// probe that hit the provider would burn the request quota and tie our availability to theirs.
/// </summary>
public sealed class WeatherReadinessHealthCheck(
    IOptions<WeatherApiOptions> apiOptions,
    IOptions<WeatherOptions> weatherOptions) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            WeatherApiOptions api = apiOptions.Value;
            WeatherOptions weather = weatherOptions.Value;

            if (string.IsNullOrWhiteSpace(api.Credential))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("WeatherAPI credential is not configured."));
            }

            IReadOnlyDictionary<string, object> data = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["location"] = weather.Location,
                ["forecastDays"] = weather.ForecastDays,
                ["regionPoints"] = weather.Region.Enabled ? weather.Region.Points.Count : 0
            };

            return Task.FromResult(HealthCheckResult.Healthy("Configuration is valid.", data));
        }
        catch (OptionsValidationException exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Configuration is invalid.", exception));
        }
    }
}
