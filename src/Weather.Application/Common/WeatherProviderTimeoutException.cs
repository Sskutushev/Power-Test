namespace Weather.Application.Common;

public sealed class WeatherProviderTimeoutException : WeatherProviderException
{
    public WeatherProviderTimeoutException(string message, Exception innerException)
        : base(WeatherFailureKind.Timeout, message, innerException)
    {
    }
}
