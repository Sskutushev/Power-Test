namespace Weather.Application.Common;

public enum WeatherFailureKind
{
    Provider,
    Timeout,
    Auth,
    RateLimit,
    Protocol,
    Configuration
}
