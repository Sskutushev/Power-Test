using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Weather.Application.Abstractions;
using Weather.Infrastructure.WeatherApi;
using Weather.Infrastructure.WeatherApi.Client;
using Weather.Infrastructure.WeatherApi.Options;

namespace Weather.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<WeatherApiOptions>()
            .Bind(configuration.GetSection("WeatherApi"))
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.Credential), "WeatherAPI credential is required.")
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

        services.AddHttpClient<IWeatherApiClient, WeatherApiClient>((serviceProvider, client) =>
            {
                WeatherApiOptions options = serviceProvider.GetRequiredService<IOptions<WeatherApiOptions>>().Value;
                client.BaseAddress = options.BaseUrl;
                client.Timeout = options.RequestTimeout;
            })
            .AddStandardResilienceHandler();

        services.AddTransient<WeatherApiProvider>();
        services.AddTransient<IWeatherProvider, CachingWeatherProvider>();

        return services;
    }
}
