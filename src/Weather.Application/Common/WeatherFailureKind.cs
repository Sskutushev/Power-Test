namespace Weather.Application.Common;

/// <summary>
/// Stable failure taxonomy consumed by the UI and by the HTTP problem mapping.
/// Callers switch on this value, never on the exception type or the provider status code.
/// </summary>
public enum WeatherFailureKind
{
    /// <summary>Unclassified failure. Default so an unmapped value never reads as a known kind.</summary>
    Unknown = 0,

    /// <summary>The provider is reachable but failed, or the circuit is open.</summary>
    Provider = 1,

    /// <summary>The provider did not answer within the configured budget.</summary>
    Timeout = 2,

    /// <summary>The provider rejected the credential.</summary>
    Auth = 3,

    /// <summary>The provider rejected the call because of its own rate limit.</summary>
    RateLimit = 4,

    /// <summary>The provider answered with a payload that does not match the expected contract.</summary>
    Protocol = 5,

    /// <summary>The application is misconfigured, so no call was attempted.</summary>
    Configuration = 6
}
