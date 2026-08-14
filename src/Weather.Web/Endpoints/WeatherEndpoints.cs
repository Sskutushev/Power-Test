using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Weather.Application.Weather.GetRegionalWeather;
using Weather.Application.Weather.GetWeatherDashboard;
using Weather.Web.Extensions;

namespace Weather.Web.Endpoints;

/// <summary>
/// Read-only HTTP API over the same MediatR use cases the Blazor UI consumes.
/// There is deliberately no second implementation of the business logic behind these routes.
/// </summary>
public static class WeatherEndpoints
{
    /// <summary>Maps the weather API routes.</summary>
    public static IEndpointRouteBuilder MapWeatherEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/weather")
            .WithTags("Weather")
            .RequireRateLimiting(WeatherRateLimiting.ApiPolicy);

        group.MapGet("/", async Task<Ok<WeatherDashboardDto>> (
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                WeatherDashboardDto dashboard = await sender.Send(new GetWeatherDashboardQuery(), cancellationToken);

                return TypedResults.Ok(dashboard);
            })
            .WithName("GetWeatherDashboard")
            .WithSummary("Moscow weather dashboard")
            .WithDescription("Current conditions, the remaining hours of today plus all of tomorrow, and a three-day forecast.")
            .Produces<WeatherDashboardDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/region", async Task<Ok<RegionalWeatherDto>> (
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                RegionalWeatherDto region = await sender.Send(new GetRegionalWeatherQuery(), cancellationToken);

                return TypedResults.Ok(region);
            })
            .WithName("GetRegionalWeather")
            .WithSummary("Territory forecast points")
            .WithDescription("Current conditions sampled across the configured map points that back the forecast map.")
            .Produces<RegionalWeatherDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }
}
