using System.Globalization;
using System.Runtime.InteropServices;

namespace Weather.Domain;

/// <summary>
/// Geographic coordinates used to address a location in the weather provider contract.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct GeoPoint(double Latitude, double Longitude)
{
    /// <summary>
    /// Formats the point as the <c>LAT,LON</c> query value required by the WeatherAPI <c>q</c> parameter.
    /// </summary>
    public string ToQueryValue()
    {
        return string.Create(CultureInfo.InvariantCulture, $"{Latitude:0.####},{Longitude:0.####}");
    }
}
