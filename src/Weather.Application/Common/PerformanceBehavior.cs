using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Application.Weather;

namespace Weather.Application.Common;

public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger,
    IOptions<WeatherOptions> options) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        TResponse response = await next(cancellationToken);
        stopwatch.Stop();
        WeatherTelemetry.QueryDuration.Record(stopwatch.Elapsed.TotalMilliseconds);

        if (stopwatch.Elapsed > options.Value.Performance.SlowQueryThreshold)
        {
            logger.LogWarning(
                "weather_query_slow {RequestName} took {ElapsedMs}ms",
                typeof(TRequest).Name,
                stopwatch.ElapsedMilliseconds);
        }

        return response;
    }
}
