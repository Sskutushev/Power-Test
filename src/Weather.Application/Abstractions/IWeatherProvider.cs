using Weather.Domain;

namespace Weather.Application.Abstractions;

public interface IWeatherProvider
{
    Task<WeatherSnapshot> GetAsync(Location location, int forecastDays, bool bypassCache, CancellationToken cancellationToken);
}
