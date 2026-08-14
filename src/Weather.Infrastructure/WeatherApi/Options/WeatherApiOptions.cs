using System.ComponentModel.DataAnnotations;

namespace Weather.Infrastructure.WeatherApi.Options;

/// <summary>
/// WeatherAPI transport settings. The credential is supplied from outside the repository
/// (user-secrets locally, environment variable in containers, secret store in CI).
/// </summary>
public sealed class WeatherApiOptions
{
    /// <summary>Provider base address. HTTPS only, even though the task letter shows an http:// URL.</summary>
    [Required]
    public Uri BaseUrl { get; init; } = new("https://api.weatherapi.com");

    /// <summary>Provider credential. Never committed, never logged.</summary>
    [Required]
    public string Credential { get; init; } = string.Empty;

    /// <summary>Per-attempt timeout applied by the resilience pipeline.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Budget for the whole call including retries.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:02:00")]
    public TimeSpan TotalTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Retry attempts for idempotent GETs. Auth and rate-limit failures are never retried.</summary>
    [Range(0, 5)]
    public int MaxRetryAttempts { get; init; } = 2;

    /// <summary>Calls <c>/v1/current.json</c> in addition to <c>/v1/forecast.json</c>, as the task letter requires.</summary>
    public bool UseSeparateCurrentEndpoint { get; init; } = true;

    /// <summary>Upper bound on concurrent provider calls while sampling the territory map.</summary>
    [Range(1, 16)]
    public int MaxRegionConcurrency { get; init; } = 4;

    /// <summary>Circuit breaker thresholds that protect the app from a degraded upstream.</summary>
    public CircuitBreakerOptions CircuitBreaker { get; init; } = new();
}

/// <summary>Circuit breaker thresholds. Exposed so tests can neutralise the breaker deterministically.</summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>Share of failed calls inside the sampling window that opens the circuit.</summary>
    [Range(0.05, 1.0)]
    public double FailureRatio { get; init; } = 0.5;

    /// <summary>Minimum calls in the window before the ratio is considered at all.</summary>
    [Range(2, 100000)]
    public int MinimumThroughput { get; init; } = 5;

    /// <summary>How long the circuit stays open before probing again.</summary>
    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Length of the window the failure ratio is measured over.</summary>
    public TimeSpan SamplingDuration { get; init; } = TimeSpan.FromSeconds(30);
}
