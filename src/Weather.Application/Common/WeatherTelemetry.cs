using System.Diagnostics.Metrics;

namespace Weather.Application.Common;

public static class WeatherTelemetry
{
    public const string MeterName = "WeatherApp";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Histogram<double> QueryDuration = Meter.CreateHistogram<double>(
        "weather.query.duration",
        "ms",
        "Duration of weather dashboard queries.");

    public static readonly Counter<long> QueryFailures = Meter.CreateCounter<long>(
        "weather.query.failures",
        description: "Number of failed weather dashboard queries.");

    public static readonly Counter<long> RefreshExecutions = Meter.CreateCounter<long>(
        "weather.refresh.executions",
        description: "Number of background weather refresh executions.");
}
