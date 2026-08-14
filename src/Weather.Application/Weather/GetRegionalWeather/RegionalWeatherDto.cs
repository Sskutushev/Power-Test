namespace Weather.Application.Weather.GetRegionalWeather;

/// <summary>UI-ready territory forecast used by the Blazor map and by <c>GET /api/weather/region</c>.</summary>
public sealed record RegionalWeatherDto(
    IReadOnlyList<RegionalWeatherPointDto> Points,
    double CenterLatitude,
    double CenterLongitude,
    int Zoom,
    DateTimeOffset UpdatedAt,
    bool IsStale);

/// <summary>A single map marker.</summary>
public sealed record RegionalWeatherPointDto(
    string Name,
    double Latitude,
    double Longitude,
    double TempC,
    double FeelsLikeC,
    string ConditionText,
    string? IconUrl,
    double WindKph,
    int Humidity);
