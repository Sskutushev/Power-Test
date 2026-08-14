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
        Location configuredLocation = new(weatherOptions.Location, weatherOptions.TimeZoneId);
        WeatherSnapshot snapshot = await provider.GetAsync(
            configuredLocation,
            weatherOptions.ForecastDays,
            request.BypassCache,
            cancellationToken);
        DateTimeOffset localNow = snapshot.LocalNow ?? timeProvider.GetUtcNow();
        IReadOnlyList<HourlyForecast> hourly = HourlyForecastSelector.Select(snapshot.Days, localNow);

        return Map(snapshot, hourly, timeProvider.GetUtcNow(), localNow);
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
