namespace Weather.Application.Common;

public class WeatherProviderException : Exception
{
    public WeatherProviderException(WeatherFailureKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public WeatherProviderException(WeatherFailureKind kind, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public WeatherFailureKind Kind { get; }
}
