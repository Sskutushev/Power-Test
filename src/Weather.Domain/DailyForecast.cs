namespace Weather.Domain;

public sealed record DailyForecast(
    DateOnly Date,
    Temperature Min,
    Temperature Max,
    WeatherCondition Condition,
    int ChanceOfRain);
