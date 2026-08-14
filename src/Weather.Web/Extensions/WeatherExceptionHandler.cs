using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Weather.Application.Common;

namespace Weather.Web.Extensions;

public sealed class WeatherExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<WeatherExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        (int status, string title) = exception is WeatherProviderException weatherException
            ? Map(weatherException.Kind)
            : (StatusCodes.Status500InternalServerError, "Unexpected server error");

        logger.LogWarning(exception, "weather_http_request_failed {TraceId} {StatusCode}", httpContext.TraceIdentifier, status);
        httpContext.Response.StatusCode = status;

        ProblemDetails problemDetails = new()
        {
            Status = status,
            Title = title,
            Type = $"https://httpstatuses.com/{status}"
        };
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (status == StatusCodes.Status429TooManyRequests)
        {
            httpContext.Response.Headers.RetryAfter = "60";
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    private static (int Status, string Title) Map(WeatherFailureKind kind)
    {
        return kind switch
        {
            WeatherFailureKind.Timeout or WeatherFailureKind.Provider => (StatusCodes.Status503ServiceUnavailable, "Weather provider unavailable"),
            WeatherFailureKind.RateLimit => (StatusCodes.Status429TooManyRequests, "Weather provider rate limit exceeded"),
            WeatherFailureKind.Auth or WeatherFailureKind.Configuration => (StatusCodes.Status500InternalServerError, "Weather service unavailable"),
            WeatherFailureKind.Protocol => (StatusCodes.Status502BadGateway, "Weather provider returned invalid data"),
            _ => (StatusCodes.Status500InternalServerError, "Weather service unavailable")
        };
    }
}
