using System.Globalization;

namespace Weather.Web.Formatting;

/// <summary>
/// Single source of truth for how weather values are rendered. Formatting is a presentation concern,
/// so it lives in the host rather than in the Domain, and every component uses these helpers instead
/// of repeating format strings.
/// </summary>
public static class WeatherFormat
{
    /// <summary>Display culture for the whole UI.</summary>
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>Signed temperature with the degree unit, for example <c>+22,3 °C</c>.</summary>
    public static string Temperature(double celsius)
    {
        return string.Create(Culture, $"{celsius:+0.#;-0.#;0} °C");
    }

    /// <summary>Signed temperature without a unit, for compact cards.</summary>
    public static string TemperatureShort(double celsius)
    {
        return string.Create(Culture, $"{celsius:+0;-0;0}°");
    }

    /// <summary>Hour of a forecast entry, for example <c>18:00</c>.</summary>
    public static string Hour(DateTimeOffset value)
    {
        return value.ToString("HH:mm", Culture);
    }

    /// <summary>Machine readable timestamp for the <c>datetime</c> attribute.</summary>
    public static string Iso(DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    /// <summary>Machine readable date for the <c>datetime</c> attribute.</summary>
    public static string Iso(DateOnly value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    /// <summary>Short weekday and date, for example <c>чт, 14 авг</c>.</summary>
    public static string DayLabel(DateOnly value)
    {
        return value.ToString("ddd, d MMM", Culture);
    }

    /// <summary>Relative day name for the first two forecast days, otherwise the weekday.</summary>
    public static string RelativeDayLabel(DateOnly value, DateOnly today)
    {
        int offset = value.DayNumber - today.DayNumber;

        return offset switch
        {
            0 => "Сегодня",
            1 => "Завтра",
            // ru-RU renders weekday names in lower case; the label sits next to "Сегодня"/"Завтра".
            _ => Capitalise(value.ToString("dddd", Culture))
        };
    }

    private static string Capitalise(string value)
    {
        return value.Length == 0 ? value : string.Create(Culture, $"{char.ToUpper(value[0], Culture)}{value[1..]}");
    }

    /// <summary>Wind speed in km/h.</summary>
    public static string Wind(double kph)
    {
        return string.Create(Culture, $"{kph:0.#} км/ч");
    }

    /// <summary>Percentage value.</summary>
    public static string Percent(int value)
    {
        return string.Create(Culture, $"{value} %");
    }

    /// <summary>Atmospheric pressure converted to the millimetres of mercury used in Russian forecasts.</summary>
    public static string Pressure(int millibars)
    {
        return string.Create(Culture, $"{millibars * 0.750062:0} мм рт. ст.");
    }

    /// <summary>UV index with its qualitative band.</summary>
    public static string UvIndex(double value)
    {
        string band = value switch
        {
            < 3 => "низкий",
            < 6 => "умеренный",
            < 8 => "высокий",
            < 11 => "очень высокий",
            _ => "экстремальный"
        };

        return string.Create(Culture, $"{value:0.#} · {band}");
    }

    /// <summary>Clock time of the last successful update, rendered in the location's local time.</summary>
    public static string UpdatedAt(DateTimeOffset updatedAt, DateTimeOffset localNow)
    {
        return updatedAt.ToOffset(localNow.Offset).ToString("HH:mm", Culture);
    }
}
