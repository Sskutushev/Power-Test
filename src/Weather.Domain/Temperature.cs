using System.Globalization;

namespace Weather.Domain;

public readonly record struct Temperature(double Celsius)
{
    public string Format(CultureInfo culture)
    {
        return string.Create(culture, $"{Celsius:+0.#;-0.#;0} °C");
    }
}
