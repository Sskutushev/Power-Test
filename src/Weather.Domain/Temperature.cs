namespace Weather.Domain;

/// <summary>
/// Temperature value object. Celsius is the only unit the provider contract and the UI use.
/// </summary>
public readonly record struct Temperature(double Celsius);
