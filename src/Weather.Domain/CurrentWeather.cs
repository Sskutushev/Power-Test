namespace Weather.Domain;

/// <summary>
/// Current conditions at the observed location.
/// </summary>
public sealed record CurrentWeather(
    Temperature Temp,
    Temperature FeelsLike,
    int Humidity,
    double WindKph,
    int WindDegree,
    double GustKph,
    int PressureMb,
    double UvIndex,
    double VisibilityKm,
    double PrecipMm,
    bool IsDay,
    WeatherCondition Condition,
    DateTimeOffset ObservedAt);
