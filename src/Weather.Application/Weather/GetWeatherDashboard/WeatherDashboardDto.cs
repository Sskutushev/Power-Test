namespace Weather.Application.Weather.GetWeatherDashboard;

/// <summary>UI-ready dashboard shared by the Blazor screen and <c>GET /api/weather</c>.</summary>
public sealed record WeatherDashboardDto(
    LocationDto Location,
    CurrentWeatherDto Current,
    IReadOnlyList<HourlyForecastDto> Hourly,
    IReadOnlyList<DailyForecastDto> Daily,
    IReadOnlyList<WeatherAdvisoryDto> Advisories,
    DateTimeOffset UpdatedAt,
    DateTimeOffset LocalNow,
    bool IsStale,
    DateTimeOffset? StaleSince);

public sealed record LocationDto(string City, string TimeZoneId);

public sealed record WeatherConditionDto(string Text, string? IconUrl, int Code);

public sealed record CurrentWeatherDto(
    double TempC,
    double FeelsLikeC,
    int Humidity,
    double WindKph,
    int WindDegree,
    double GustKph,
    int PressureMb,
    double UvIndex,
    double VisibilityKm,
    double PrecipMm,
    bool IsDay,
    WeatherConditionDto Condition,
    DateTimeOffset ObservedAt);

public sealed record HourlyForecastDto(
    DateTimeOffset LocalTime,
    double TempC,
    double FeelsLikeC,
    WeatherConditionDto Condition,
    int ChanceOfRain,
    int ChanceOfSnow,
    double PrecipMm,
    double WindKph,
    int WindDegree,
    double UvIndex,
    bool IsDay);

/// <summary>
/// A forecast day. <see cref="Hours"/> carries the day's full 24 hours for the expandable detail view;
/// the screen's required "remaining today plus all of tomorrow" window lives in
/// <see cref="WeatherDashboardDto.Hourly"/> instead.
/// </summary>
public sealed record DailyForecastDto(
    DateOnly Date,
    double MinC,
    double MaxC,
    WeatherConditionDto Condition,
    int ChanceOfRain,
    int ChanceOfSnow,
    double TotalPrecipMm,
    double MaxWindKph,
    double UvIndex,
    AstroDto Astro,
    IReadOnlyList<HourlyForecastDto> Hours);

/// <summary>Sun and moon data for a forecast day.</summary>
public sealed record AstroDto(TimeOnly? Sunrise, TimeOnly? Sunset, string? MoonPhase, TimeSpan? DayLength);

/// <summary>
/// A short, actionable sentence derived from the forecast — the difference between reporting numbers and
/// answering the question the visitor actually opened the page with.
/// </summary>
public sealed record WeatherAdvisoryDto(WeatherAdvisoryKind Kind, string Text);

/// <summary>Classification used to pick the advisory's icon and accent.</summary>
public enum WeatherAdvisoryKind
{
    /// <summary>Nothing notable; the day is calm.</summary>
    Calm = 0,

    /// <summary>Rain is expected within the visible window.</summary>
    Rain = 1,

    /// <summary>Snow is expected within the visible window.</summary>
    Snow = 2,

    /// <summary>Wind makes it feel materially colder, or gusts are strong.</summary>
    Wind = 3,

    /// <summary>The UV index reaches a level worth avoiding.</summary>
    Ultraviolet = 4,

    /// <summary>What to wear, derived from the felt temperature.</summary>
    Clothing = 5,

    /// <summary>Daylight boundaries.</summary>
    Daylight = 6
}
