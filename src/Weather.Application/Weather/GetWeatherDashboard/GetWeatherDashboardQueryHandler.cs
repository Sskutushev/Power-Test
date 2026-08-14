using MediatR;
using Microsoft.Extensions.Options;
using Weather.Application.Abstractions;
using Weather.Domain;

namespace Weather.Application.Weather.GetWeatherDashboard;

public sealed class GetWeatherDashboardQueryHandler(
    IWeatherProvider provider,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : IRequestHandler<GetWeatherDashboardQuery, WeatherDashboardDto>
{
    public async Task<WeatherDashboardDto> Handle(GetWeatherDashboardQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        WeatherOptions weatherOptions = options.Value;
        Location location = Resolve(weatherOptions, request);
        WeatherSnapshot snapshot = await provider.GetAsync(
            location,
            weatherOptions.ForecastDays,
            request.BypassCache,
            cancellationToken);
        DateTimeOffset localNow = snapshot.LocalNow ?? FallbackLocalNow(weatherOptions.TimeZoneId);
        IReadOnlyList<HourlyForecast> hourly = HourlyForecastSelector.Select(snapshot.Days, localNow);

        return Map(snapshot, hourly, timeProvider.GetUtcNow(), localNow);
    }

    /// <summary>
    /// The configured location wins unless the caller supplied both coordinates. The display name is left
    /// to the provider in that case, because we have no name for an arbitrary point.
    /// </summary>
    private static Location Resolve(WeatherOptions options, GetWeatherDashboardQuery request)
    {
        return request is { Latitude: { } latitude, Longitude: { } longitude }
            ? new Location(options.Location, options.TimeZoneId, new GeoPoint(latitude, longitude))
            : new Location(options.Location, options.TimeZoneId, new GeoPoint(options.Latitude, options.Longitude));
    }

    /// <summary>
    /// Converts the injected clock into the configured location's wall clock. Returning a raw UTC value here
    /// would filter "remaining hours today" against the wrong hour on a UTC server or container.
    /// </summary>
    private DateTimeOffset FallbackLocalNow(string timeZoneId)
    {
        DateTimeOffset utcNow = timeProvider.GetUtcNow();

        try
        {
            return TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return utcNow;
        }
    }

    private static WeatherDashboardDto Map(
        WeatherSnapshot snapshot,
        IReadOnlyList<HourlyForecast> hourly,
        DateTimeOffset updatedAt,
        DateTimeOffset localNow)
    {
        return new WeatherDashboardDto(
            new LocationDto(snapshot.Location.City, snapshot.Location.TimeZoneId),
            Map(snapshot.Current),
            hourly.Select(Map).ToArray(),
            snapshot.Days.Select(day => Map(day.Daily)).ToArray(),
            updatedAt,
            localNow,
            snapshot.IsStale,
            snapshot.StaleSince);
    }

    private static CurrentWeatherDto Map(CurrentWeather current)
    {
        return new CurrentWeatherDto(
            current.Temp.Celsius,
            current.FeelsLike.Celsius,
            current.Humidity,
            current.WindKph,
            current.PressureMb,
            current.UvIndex,
            Map(current.Condition),
            current.ObservedAt);
    }

    private static HourlyForecastDto Map(HourlyForecast forecast)
    {
        return new HourlyForecastDto(
            forecast.LocalTime,
            forecast.Temp.Celsius,
            Map(forecast.Condition),
            forecast.ChanceOfRain,
            forecast.WindKph);
    }

    private static DailyForecastDto Map(DailyForecast forecast)
    {
        return new DailyForecastDto(
            forecast.Date,
            forecast.Min.Celsius,
            forecast.Max.Celsius,
            Map(forecast.Condition),
            forecast.ChanceOfRain);
    }

    private static WeatherConditionDto Map(WeatherCondition condition)
    {
        return new WeatherConditionDto(condition.Text, condition.IconUrl, condition.Code);
    }
}
