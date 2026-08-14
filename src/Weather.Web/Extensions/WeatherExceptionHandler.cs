using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Weather.Application.Common;

namespace Weather.Web.Extensions;

/// <summary>
/// Translates the application failure taxonomy into stable <see cref="ProblemDetails"/> responses.
/// Nothing provider-specific leaks: no stack traces, no internal type names, no hint that a failure
/// was caused by the credential.
/// </summary>
public sealed class WeatherExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<WeatherExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // A client that walked away is not a server error and must not be reported as one.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        (int status, string title, string detail) = exception is WeatherProviderException weatherException
            ? Map(weatherException.Kind)
            : (StatusCodes.Status500InternalServerError, "Unexpected server error", "The request could not be completed.");

        logger.LogWarning(
            exception,
            "weather_http_request_failed {TraceId} {StatusCode} {Path}",
            httpContext.TraceIdentifier,
            status,
            httpContext.Request.Path.Value);

        httpContext.Response.StatusCode = status;

        if (status == StatusCodes.Status429TooManyRequests)
        {
            httpContext.Response.Headers.RetryAfter = "60";
        }

        ProblemDetails problemDetails = new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{status}"
        };
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    private static (int Status, string Title, string Detail) Map(WeatherFailureKind kind)
    {
        return kind switch
        {
            WeatherFailureKind.Timeout => (StatusCodes.Status504GatewayTimeout, "Weather provider timed out", "The upstream weather provider did not answer in time."),
            WeatherFailureKind.Provider => (StatusCodes.Status503ServiceUnavailable, "Weather provider unavailable", "The upstream weather provider is temporarily unavailable."),
            WeatherFailureKind.RateLimit => (StatusCodes.Status429TooManyRequests, "Weather provider rate limit exceeded", "Too many upstream requests. Retry later."),
            WeatherFailureKind.Protocol => (StatusCodes.Status502BadGateway, "Weather provider returned invalid data", "The upstream response did not match the expected contract."),
            WeatherFailureKind.Auth or WeatherFailureKind.Configuration => (StatusCodes.Status500InternalServerError, "Weather service unavailable", "The service is not able to serve weather data right now."),
            _ => (StatusCodes.Status500InternalServerError, "Weather service unavailable", "The service is not able to serve weather data right now.")
        };
    }
}
