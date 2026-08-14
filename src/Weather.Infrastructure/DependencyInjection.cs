using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Weather.Application.Abstractions;
using Weather.Infrastructure.WeatherApi;
using Weather.Infrastructure.WeatherApi.Client;
using Weather.Infrastructure.WeatherApi.Options;

namespace Weather.Infrastructure;

/// <summary>
/// Composition root of the WeatherAPI adapter: options, typed client, resilience, cache, providers.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Registers everything the Application layer's provider contracts need.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<WeatherApiOptions>()
            .Bind(configuration.GetSection("WeatherApi"))
            .ValidateDataAnnotations()
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Credential),
                "WeatherApi:Credential is required. Supply it through user-secrets, an environment variable, or a CI secret.")
            .Validate(
                options => IsTransportAcceptable(options.BaseUrl),
                "WeatherApi:BaseUrl must use HTTPS: the credential travels in the query string.")
            .Validate(
                options => options.TotalTimeout >= options.RequestTimeout,
                "WeatherApi:TotalTimeout must not be shorter than WeatherApi:RequestTimeout.")
            .ValidateOnStart();

        services
            .AddOptions<WeatherCacheOptions>()
            .Bind(configuration.GetSection("Weather:Cache"))
            .ValidateOnStart();

        string? redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
        }

        services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = 1024 * 1024;
            options.MaximumKeyLength = 256;
        });

        AddWeatherApiClient(services);

        services.AddTransient<WeatherApiProvider>();
        services.AddTransient<WeatherApiRegionalProvider>();
        services.AddTransient<IWeatherProvider, CachingWeatherProvider>();
        services.AddTransient<IRegionalWeatherProvider, CachingRegionalWeatherProvider>();

        return services;
    }

    private static void AddWeatherApiClient(IServiceCollection services)
    {
        services
            .AddHttpClient<IWeatherApiClient, WeatherApiClient>((serviceProvider, client) =>
            {
                WeatherApiOptions options = serviceProvider.GetRequiredService<IOptions<WeatherApiOptions>>().Value;
                client.BaseAddress = options.BaseUrl;

                // Timeouts belong to the resilience pipeline. A hard HttpClient timeout would fire across
                // the whole retry sequence and surface as a raw TaskCanceledException instead of a
                // classified timeout, so it is disabled here on purpose.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddStandardResilienceHandler()
            .Configure((handler, serviceProvider) =>
            {
                WeatherApiOptions options = serviceProvider.GetRequiredService<IOptions<WeatherApiOptions>>().Value;

                handler.AttemptTimeout.Timeout = options.RequestTimeout;
                handler.TotalRequestTimeout.Timeout = options.TotalTimeout;

                // The standard handler rejects an attempt count below one, so "no retries" is expressed by
                // refusing to handle anything rather than by a zero count.
                bool retriesEnabled = options.MaxRetryAttempts > 0;
                handler.Retry.MaxRetryAttempts = retriesEnabled ? options.MaxRetryAttempts : 1;
                handler.Retry.BackoffType = DelayBackoffType.Exponential;
                handler.Retry.UseJitter = true;

                // 401/403 will not become valid on retry and 429 only gets worse, so both are excluded.
                handler.Retry.ShouldHandle = arguments =>
                    ValueTask.FromResult(retriesEnabled && ShouldRetry(arguments.Outcome));

                // The sampling window must stay at least twice the attempt timeout, so the configured
                // value is raised rather than trusted blindly.
                handler.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(Math.Max(
                    options.CircuitBreaker.SamplingDuration.TotalSeconds,
                    options.RequestTimeout.TotalSeconds * 2));
                handler.CircuitBreaker.FailureRatio = options.CircuitBreaker.FailureRatio;
                handler.CircuitBreaker.MinimumThroughput = options.CircuitBreaker.MinimumThroughput;
                handler.CircuitBreaker.BreakDuration = options.CircuitBreaker.BreakDuration;
            });
    }

    /// <summary>
    /// The real provider must be reached over HTTPS — the credential is a query parameter, so plain HTTP
    /// would leak it to every proxy on the path. Loopback is the single exception: contract and integration
    /// tests point the client at a local stub server, and requiring TLS there would only buy a self-signed
    /// certificate dance with no security benefit.
    /// </summary>
    private static bool IsTransportAcceptable(Uri baseUrl)
    {
        return string.Equals(baseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || baseUrl.IsLoopback;
    }

    private static bool ShouldRetry(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is not null)
        {
            return outcome.Exception is HttpRequestException or TimeoutException
                || outcome.Exception is TaskCanceledException;
        }

        HttpResponseMessage? response = outcome.Result;

        if (response is null)
        {
            return false;
        }

        int status = (int)response.StatusCode;

        return status == 408 || (status >= 500 && status <= 599);
    }
}
