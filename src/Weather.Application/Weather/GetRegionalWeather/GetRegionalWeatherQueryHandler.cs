using MediatR;
using Microsoft.Extensions.Options;
using Weather.Application.Abstractions;
using Weather.Domain;

namespace Weather.Application.Weather.GetRegionalWeather;

/// <summary>
/// Builds the territory map payload from configuration plus one provider sweep.
/// </summary>
public sealed class GetRegionalWeatherQueryHandler(
    IRegionalWeatherProvider provider,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : IRequestHandler<GetRegionalWeatherQuery, RegionalWeatherDto>
{
    /// <inheritdoc />
    public async Task<RegionalWeatherDto> Handle(GetRegionalWeatherQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WeatherOptions weatherOptions = options.Value;
        RegionOptions region = weatherOptions.Region;

        if (!region.Enabled || region.Points.Count == 0)
        {
            return Empty(weatherOptions, timeProvider.GetUtcNow());
        }

        Location[] locations = region.Points
            .Select(point => new Location(
                point.Name,
                weatherOptions.TimeZoneId,
                new GeoPoint(point.Latitude, point.Longitude)))
            .ToArray();

        RegionalWeatherSnapshot snapshot = await provider.GetAsync(locations, request.BypassCache, cancellationToken);

        return new RegionalWeatherDto(
            snapshot.Points.Select(Map).ToArray(),
            weatherOptions.Latitude,
            weatherOptions.Longitude,
            region.Zoom,
            timeProvider.GetUtcNow(),
            snapshot.IsStale);
    }

    private static RegionalWeatherDto Empty(WeatherOptions options, DateTimeOffset updatedAt)
    {
        return new RegionalWeatherDto([], options.Latitude, options.Longitude, options.Region.Zoom, updatedAt, false);
    }

    private static RegionalWeatherPointDto Map(RegionalWeatherPoint point)
    {
        return new RegionalWeatherPointDto(
            point.Name,
            point.Coordinates.Latitude,
            point.Coordinates.Longitude,
            point.Temp.Celsius,
            point.FeelsLike.Celsius,
            point.Condition.Text,
            point.Condition.IconUrl,
            point.WindKph,
            point.Humidity);
    }
}
