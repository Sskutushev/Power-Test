using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Weather.Application.Common;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("weather_request_started {RequestName}", requestName);

        try
        {
            TResponse response = await next(cancellationToken);
            stopwatch.Stop();
            logger.LogInformation("weather_request_completed {RequestName} in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            WeatherTelemetry.QueryFailures.Add(1, new KeyValuePair<string, object?>("request", requestName));
            logger.LogWarning(exception, "weather_request_failed {RequestName} in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
