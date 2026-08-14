namespace Weather.Domain;

public sealed record CurrentWeather(
    Temperature Temp,
    Temperature FeelsLike,
    int Humidity,
    double WindKph,
    int PressureMb,
    double UvIndex,
    WeatherCondition Condition,
    DateTimeOffset ObservedAt);
