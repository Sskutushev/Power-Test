namespace Weather.Domain;

/// <summary>
/// One hour of the forecast. The extra fields beyond temperature are what let the UI answer questions
/// ("will it rain", "why does it feel colder") instead of only reporting numbers.
/// </summary>
public sealed record HourlyForecast(
    DateTimeOffset LocalTime,
    Temperature Temp,
    Temperature FeelsLike,
    WeatherCondition Condition,
    int ChanceOfRain,
    int ChanceOfSnow,
    double PrecipMm,
    double WindKph,
    int WindDegree,
    double UvIndex,
    bool IsDay);
