namespace Weather.Domain;

public sealed record WeatherCondition(string Text, string? IconUrl, int Code);
