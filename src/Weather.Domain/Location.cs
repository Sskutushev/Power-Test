namespace Weather.Domain;

/// <summary>
/// A weather location addressed by coordinates, with a display name and the local time zone.
/// </summary>
public sealed record Location(string City, string TimeZoneId, GeoPoint Coordinates);
