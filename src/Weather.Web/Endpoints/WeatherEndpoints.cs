using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Weather.Application.Weather.GetWeatherDashboard;

namespace Weather.Web.Endpoints;

public static class WeatherEndpoints
{
    public static IEndpointRouteBuilder MapWeatherEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/weather", async Task<Ok<WeatherDashboardDto>> (ISender sender, CancellationToken cancellationToken) =>
            {
                WeatherDashboardDto dashboard = await sender.Send(new GetWeatherDashboardQuery(), cancellationToken);
                return TypedResults.Ok(dashboard);
            })
            .RequireRateLimiting("weather-api")
            .WithName("GetWeather");

        return endpoints;
    }
}
