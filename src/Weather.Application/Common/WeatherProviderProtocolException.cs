namespace Weather.Application.Common;

public sealed class WeatherProviderProtocolException : WeatherProviderException
{
    public WeatherProviderProtocolException(string message, Exception innerException)
        : base(WeatherFailureKind.Protocol, message, innerException)
    {
    }
}
