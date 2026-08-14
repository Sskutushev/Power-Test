using MediatR;

namespace Weather.Application.Weather.GetWeatherDashboard;

/// <summary>
/// Reads the dashboard for the configured location, or for explicit coordinates when the visitor has
/// opted into sharing their position. The assignment fixes the location to Moscow, so the override is
/// opt-in and the configured location remains the default.
/// </summary>
public sealed record GetWeatherDashboardQuery(
    bool BypassCache = false,
    double? Latitude = null,
    double? Longitude = null) : IRequest<WeatherDashboardDto>;
