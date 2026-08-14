namespace Weather.Application.Common;

public sealed class WeatherConfigurationException : WeatherProviderException
{
    public WeatherConfigurationException(string message)
        : base(WeatherFailureKind.Configuration, message)
    {
    }
}
