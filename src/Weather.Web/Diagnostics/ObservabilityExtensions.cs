using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Weather.Application.Common;

namespace Weather.Web.Diagnostics;

/// <summary>
/// OpenTelemetry wiring. The exporter stays vendor neutral: traces and metrics go to an OTLP endpoint
/// when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set and are simply not exported otherwise, so the app
/// never depends on a collector being present.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>Registers tracing and metrics for the app, ASP.NET Core, HttpClient, and the runtime.</summary>
    public static IServiceCollection AddWeatherObservability(this IServiceCollection services, IHostEnvironment environment)
    {
        IOpenTelemetryBuilder builder = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: "weather-web",
                serviceVersion: typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                serviceInstanceId: environment.ApplicationName))
            .WithTracing(tracing => tracing
                .AddSource(WeatherTelemetry.ActivitySourceName)
                .AddAspNetCoreInstrumentation(options => options.Filter = IsTraceable)
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddMeter(WeatherTelemetry.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
        {
            builder.UseOtlpExporter();
        }

        return services;
    }

    /// <summary>Health probes and static assets would otherwise dominate the trace volume.</summary>
    private static bool IsTraceable(HttpContext context)
    {
        PathString path = context.Request.Path;

        return !path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWithSegments("/lib", StringComparison.OrdinalIgnoreCase);
    }
}
