using MediatR;

namespace Weather.Application.Weather.GetRegionalWeather;

/// <summary>
/// Reads current conditions across the configured territory points for the forecast map.
/// </summary>
public sealed record GetRegionalWeatherQuery(bool BypassCache = false) : IRequest<RegionalWeatherDto>;
