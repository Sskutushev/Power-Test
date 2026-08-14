namespace Weather.Domain;

/// <summary>
/// Current weather observed at a single map point of the surrounding territory.
/// </summary>
public sealed record RegionalWeatherPoint(
    string Name,
    GeoPoint Coordinates,
    Temperature Temp,
    Temperature FeelsLike,
    WeatherCondition Condition,
    double WindKph,
    int Humidity);
