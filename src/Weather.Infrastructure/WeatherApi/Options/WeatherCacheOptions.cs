namespace Weather.Infrastructure.WeatherApi.Options;

public sealed class WeatherCacheOptions
{
    public TimeSpan LocalCacheExpiration { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan Expiration { get; init; } = TimeSpan.FromMinutes(10);

    public TimeSpan StaleExpiration { get; init; } = TimeSpan.FromHours(1);
}
