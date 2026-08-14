using Weather.Domain;

namespace Weather.Application.Weather.GetWeatherDashboard;

public static class HourlyForecastSelector
{
    public static IReadOnlyList<HourlyForecast> Select(IReadOnlyList<DayForecast> days, DateTimeOffset localNow)
    {
        if (days.Count == 0)
        {
            return [];
        }

        var today = DateOnly.FromDateTime(localNow.DateTime);
        DateOnly tomorrow = today.AddDays(1);

        return days
            // A day deserialised from a cache entry written by an earlier version can have no hours at
            // all, so a missing collection is treated the same as an empty one.
            .SelectMany(day => day.Hours ?? [])
            .Where(hour => ShouldInclude(hour.LocalTime, today, tomorrow, localNow.Hour))
            .GroupBy(hour => new DateTime(hour.LocalTime.Year, hour.LocalTime.Month, hour.LocalTime.Day, hour.LocalTime.Hour, 0, 0))
            .Select(group => group.OrderBy(hour => hour.LocalTime).First())
            .OrderBy(hour => hour.LocalTime.Date)
            .ThenBy(hour => hour.LocalTime.Hour)
            .ToArray();
    }

    private static bool ShouldInclude(DateTimeOffset hourTime, DateOnly today, DateOnly tomorrow, int currentHour)
    {
        var date = DateOnly.FromDateTime(hourTime.DateTime);

        if (date == today)
        {
            return hourTime.Hour >= currentHour;
        }

        return date == tomorrow;
    }
}
