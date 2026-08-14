namespace Weather.Domain;

public sealed record HourlyForecast(
    DateTimeOffset LocalTime,
    Temperature Temp,
    WeatherCondition Condition,
    int ChanceOfRain,
    double WindKph);
