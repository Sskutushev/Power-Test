namespace Weather.Application.Common;

public sealed class WeatherProviderRateLimitException : WeatherProviderException
{
    public WeatherProviderRateLimitException(string message)
        : base(WeatherFailureKind.RateLimit, message)
    {
    }
}
