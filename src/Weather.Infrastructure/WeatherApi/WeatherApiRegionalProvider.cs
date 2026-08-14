using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Application.Abstractions;
using Weather.Application.Common;
using Weather.Domain;
using Weather.Infrastructure.WeatherApi.Client;
using Weather.Infrastructure.WeatherApi.Contracts;
using Weather.Infrastructure.WeatherApi.Mapping;
using Weather.Infrastructure.WeatherApi.Options;

namespace Weather.Infrastructure.WeatherApi;

/// <summary>
/// Samples current conditions across the territory points that back the forecast map.
/// A point that fails is dropped rather than failing the whole map; if every point fails the
/// caller gets the classified failure so the UI can show its error state.
/// </summary>
internal sealed class WeatherApiRegionalProvider(
    IWeatherApiClient client,
    IOptions<WeatherApiOptions> options,
    ILogger<WeatherApiRegionalProvider> logger) : IRegionalWeatherProvider
{
    /// <inheritdoc />
    public async Task<RegionalWeatherSnapshot> GetAsync(
        IReadOnlyList<Location> points,
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        if (points.Count == 0)
        {
            return new RegionalWeatherSnapshot([]);
        }

        using Activity? activity = WeatherTelemetry.ActivitySource.StartActivity("weather.provider.region");
        activity?.SetTag("weather.region.points", points.Count);

        using SemaphoreSlim gate = new(options.Value.MaxRegionConcurrency);
        IEnumerable<Task<PointResult>> requests = points.Select(point => GetPointAsync(point, gate, cancellationToken));
        PointResult[] results = await Task.WhenAll(requests).ConfigureAwait(false);

        RegionalWeatherPoint[] mapped = results
            .Where(result => result.Point is not null)
            .Select(result => result.Point!)
            .ToArray();

        if (mapped.Length == 0)
        {
            Exception failure = results.Select(result => result.Failure).First(exception => exception is not null)!;
            throw ProviderFailureMapper.Map(failure);
        }

        if (mapped.Length != points.Count)
        {
            logger.LogWarning(
                "weather_region_partial {Available} of {Requested} points resolved",
                mapped.Length,
                points.Count);
        }

        return new RegionalWeatherSnapshot(mapped);
    }

    private async Task<PointResult> GetPointAsync(Location location, SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            WeatherApiCurrentResponse response = await client
                .GetCurrentAsync(location.Coordinates.ToQueryValue(), cancellationToken)
                .ConfigureAwait(false);

            return new PointResult(WeatherApiMapper.MapRegionPoint(location, response), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "weather_region_point_failed {PointName}", location.City);
            return new PointResult(null, exception);
        }
        finally
        {
            gate.Release();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct PointResult(RegionalWeatherPoint? Point, Exception? Failure);
}
