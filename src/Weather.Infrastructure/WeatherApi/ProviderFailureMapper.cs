using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Weather.Application.Common;

namespace Weather.Infrastructure.WeatherApi;

/// <summary>
/// Single place that turns transport and resilience-pipeline failures into the Application taxonomy.
/// Nothing outside Infrastructure should ever see a Polly, HttpClient, or System.Text.Json exception.
/// </summary>
internal static class ProviderFailureMapper
{
    /// <summary>Classifies an outbound failure.</summary>
    public static WeatherProviderException Map(Exception exception)
    {
        WeatherProviderException mapped = MapCore(exception);
        WeatherTelemetry.ProviderFailures.Add(1, new KeyValuePair<string, object?>("kind", mapped.Kind.ToString()));

        return mapped;
    }

    private static WeatherProviderException MapCore(Exception exception)
    {
        return exception switch
        {
            WeatherProviderException already => already,
            OptionsValidationException => new WeatherConfigurationException("WeatherAPI is not configured correctly."),
            TimeoutRejectedException => new WeatherProviderTimeoutException("WeatherAPI request timed out.", exception),
            TaskCanceledException { InnerException: TimeoutException } => new WeatherProviderTimeoutException("WeatherAPI request timed out.", exception),
            TaskCanceledException => new WeatherProviderTimeoutException("WeatherAPI request timed out.", exception),
            BrokenCircuitException => new WeatherProviderException(WeatherFailureKind.Provider, "WeatherAPI circuit is open after repeated failures.", exception),
            JsonException => new WeatherProviderProtocolException("WeatherAPI response could not be parsed.", exception),
            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } => new WeatherProviderAuthException("WeatherAPI credentials were rejected."),
            HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => new WeatherProviderRateLimitException("WeatherAPI rate limit was exceeded."),
            HttpRequestException => new WeatherProviderException(WeatherFailureKind.Provider, "WeatherAPI request failed.", exception),
            _ => new WeatherProviderException(WeatherFailureKind.Unknown, "WeatherAPI request failed unexpectedly.", exception)
        };
    }
}
