namespace Weather.Application.Common;

public sealed class WeatherProviderAuthException : WeatherProviderException
{
    public WeatherProviderAuthException(string message)
        : base(WeatherFailureKind.Auth, message)
    {
    }
}
