namespace Weather.Domain;

/// <summary>
/// One forecast day, with the totals and astro data needed to expand it into a detail view.
/// </summary>
public sealed record DailyForecast(
    DateOnly Date,
    Temperature Min,
    Temperature Max,
    WeatherCondition Condition,
    int ChanceOfRain,
    int ChanceOfSnow,
    double TotalPrecipMm,
    double MaxWindKph,
    double UvIndex,
    AstroInfo Astro);
