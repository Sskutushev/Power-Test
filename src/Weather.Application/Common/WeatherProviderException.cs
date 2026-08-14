namespace Weather.Application.Common;

/// <summary>
/// Base of the controlled provider failure taxonomy. Everything the UI and the HTTP API can render
/// is expressed through <see cref="Kind"/>; raw transport exceptions never cross the Application boundary.
/// </summary>
public class WeatherProviderException : Exception
{
    /// <summary>Creates a failure of the given kind.</summary>
    public WeatherProviderException(WeatherFailureKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    /// <summary>Creates a failure of the given kind wrapping the transport cause.</summary>
    public WeatherProviderException(WeatherFailureKind kind, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>Stable failure classification.</summary>
    public WeatherFailureKind Kind { get; }
}
