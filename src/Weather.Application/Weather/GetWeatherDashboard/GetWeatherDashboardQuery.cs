using MediatR;

namespace Weather.Application.Weather.GetWeatherDashboard;

public sealed record GetWeatherDashboardQuery(bool BypassCache = false) : IRequest<WeatherDashboardDto>;
