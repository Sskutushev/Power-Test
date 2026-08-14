namespace Weather.Domain;

public sealed record DayForecast(
    DateOnly Date,
    IReadOnlyList<HourlyForecast> Hours,
    DailyForecast Daily);
