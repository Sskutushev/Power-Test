using System.ComponentModel.DataAnnotations;

namespace Weather.Infrastructure.WeatherApi.Options;

public sealed class WeatherApiOptions
{
    [Required]
    public Uri BaseUrl { get; init; } = new("https://api.weatherapi.com");

    [Required]
    public string Credential { get; init; } = string.Empty;

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public bool UseSeparateCurrentEndpoint { get; init; } = true;
}
