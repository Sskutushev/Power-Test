using System.Globalization;
using Weather.Domain;

namespace Weather.Application.Weather.GetWeatherDashboard;

/// <summary>
/// Turns the forecast into the handful of sentences people actually open a weather app for: will it rain,
/// how should I dress, why does it feel colder than it says, when does it get dark.
/// <para>
/// This is a pure function over data the provider already returned — no extra calls, no clock access — so
/// it is exhaustively testable and cannot drift from what the screen shows.
/// </para>
/// </summary>
public static class WeatherAdvisor
{
    /// <summary>Hours ahead considered when looking for the next precipitation.</summary>
    private const int PrecipitationHorizonHours = 12;

    /// <summary>Rain chance from which an umbrella is worth mentioning.</summary>
    private const int MeaningfulChance = 40;

    /// <summary>Felt-temperature gap that is worth explaining to the reader.</summary>
    private const double NotableFeelsLikeDelta = 3;

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>Builds the advisory list, most useful first.</summary>
    public static IReadOnlyList<WeatherAdvisoryDto> Advise(
        CurrentWeather current,
        IReadOnlyList<HourlyForecast> hourly,
        IReadOnlyList<DailyForecast> daily,
        DateTimeOffset localNow)
    {
        List<WeatherAdvisoryDto> advisories = [];

        AddPrecipitation(advisories, hourly, localNow);
        AddFeelsLike(advisories, current);
        AddUltraviolet(advisories, hourly, localNow);
        AddDaylight(advisories, daily, localNow);
        AddClothing(advisories, current);

        if (advisories.Count == 0)
        {
            advisories.Add(new WeatherAdvisoryDto(WeatherAdvisoryKind.Calm, "Спокойная погода — без осадков и сильного ветра."));
        }

        return advisories;
    }

    private static void AddPrecipitation(List<WeatherAdvisoryDto> advisories, IReadOnlyList<HourlyForecast> hourly, DateTimeOffset localNow)
    {
        HourlyForecast[] horizon = hourly
            .Where(hour => hour.LocalTime >= localNow.AddHours(-1) && hour.LocalTime <= localNow.AddHours(PrecipitationHorizonHours))
            .OrderBy(hour => hour.LocalTime)
            .ToArray();

        if (horizon.Length == 0)
        {
            return;
        }

        HourlyForecast? snow = horizon.FirstOrDefault(IsSnowy);
        HourlyForecast? rain = horizon.FirstOrDefault(IsRainy);

        // Snow wins when both are forecast: it changes what you wear, not just whether you take an umbrella.
        if (snow is not null && (rain is null || snow.LocalTime <= rain.LocalTime))
        {
            advisories.Add(new WeatherAdvisoryDto(
                WeatherAdvisoryKind.Snow,
                Describe(snow, localNow, "Снег", "обувь по погоде")));
            return;
        }

        if (rain is not null)
        {
            advisories.Add(new WeatherAdvisoryDto(
                WeatherAdvisoryKind.Rain,
                Describe(rain, localNow, "Дождь", "зонт пригодится")));
            return;
        }

        advisories.Add(new WeatherAdvisoryDto(
            WeatherAdvisoryKind.Calm,
            $"Ближайшие {PrecipitationHorizonHours} часов без осадков."));
    }

    private static string Describe(HourlyForecast hour, DateTimeOffset localNow, string what, string advice)
    {
        int inHours = (int)Math.Round((hour.LocalTime - localNow).TotalHours, MidpointRounding.AwayFromZero);
        string when = inHours <= 0
            ? "уже идёт"
            : string.Create(Culture, $"около {hour.LocalTime:HH:mm}");

        return string.Create(Culture, $"{what} {when} — {advice}. Вероятность {hour.ChanceOfRain + hour.ChanceOfSnow} %.");
    }

    private static void AddFeelsLike(List<WeatherAdvisoryDto> advisories, CurrentWeather current)
    {
        double delta = current.FeelsLike.Celsius - current.Temp.Celsius;

        if (delta <= -NotableFeelsLikeDelta)
        {
            advisories.Add(new WeatherAdvisoryDto(
                WeatherAdvisoryKind.Wind,
                string.Create(Culture, $"Ощущается на {Math.Abs(delta):0.#}° холоднее — ветер {current.WindKph:0} км/ч.")));
            return;
        }

        if (delta >= NotableFeelsLikeDelta)
        {
            advisories.Add(new WeatherAdvisoryDto(
                WeatherAdvisoryKind.Wind,
                string.Create(Culture, $"Ощущается на {delta:0.#}° теплее из-за влажности {current.Humidity} %.")));
            return;
        }

        // Gusts matter even when the felt temperature does not move much.
        if (current.GustKph >= 50)
        {
            advisories.Add(new WeatherAdvisoryDto(
                WeatherAdvisoryKind.Wind,
                string.Create(Culture, $"Порывы до {current.GustKph:0} км/ч — держите зонт крепче.")));
        }
    }

    private static void AddUltraviolet(List<WeatherAdvisoryDto> advisories, IReadOnlyList<HourlyForecast> hourly, DateTimeOffset localNow)
    {
        HourlyForecast[] today = hourly
            .Where(hour => hour.LocalTime.Date == localNow.Date && hour.UvIndex >= 6)
            .OrderBy(hour => hour.LocalTime)
            .ToArray();

        if (today.Length == 0)
        {
            return;
        }

        string from = today[0].LocalTime.ToString("HH:mm", Culture);
        string to = today[^1].LocalTime.AddHours(1).ToString("HH:mm", Culture);

        advisories.Add(new WeatherAdvisoryDto(
            WeatherAdvisoryKind.Ultraviolet,
            $"Высокий УФ с {from} до {to} — на солнце лучше недолго."));
    }

    private static void AddDaylight(List<WeatherAdvisoryDto> advisories, IReadOnlyList<DailyForecast> daily, DateTimeOffset localNow)
    {
        DailyForecast? today = daily.FirstOrDefault(day => day.Date == DateOnly.FromDateTime(localNow.DateTime));

        // Astro can be absent on a snapshot deserialised from an older cache entry.
        if (today?.Astro?.Sunset is not { } sunset)
        {
            return;
        }

        var now = TimeOnly.FromDateTime(localNow.DateTime);

        if (now < sunset)
        {
            TimeSpan left = sunset - now;

            if (left <= TimeSpan.FromHours(3))
            {
                advisories.Add(new WeatherAdvisoryDto(
                    WeatherAdvisoryKind.Daylight,
                    $"Закат в {sunset:HH\\:mm} — светло ещё около {FormatDuration(left)}."));
            }

            return;
        }

        if (today.Astro?.Sunrise is { } sunrise)
        {
            advisories.Add(new WeatherAdvisoryDto(
                WeatherAdvisoryKind.Daylight,
                $"Уже стемнело. Рассвет завтра около {sunrise:HH\\:mm}."));
        }
    }

    private static void AddClothing(List<WeatherAdvisoryDto> advisories, CurrentWeather current)
    {
        string advice = current.FeelsLike.Celsius switch
        {
            <= -20 => "Экстремальный холод: многослойно, закрытое лицо, недолгие выходы.",
            <= -10 => "Сильный мороз: пуховик, шапка, перчатки.",
            <= 0 => "Мороз: тёплая куртка и шапка.",
            <= 8 => "Прохладно: куртка и что-то тёплое под низ.",
            <= 15 => "Свежо: ветровка или лёгкая куртка.",
            <= 22 => "Комфортно: лёгкая одежда, к вечеру пригодится кофта.",
            <= 28 => "Тепло: лёгкая одежда, пейте воду.",
            _ => "Жарко: лёгкая одежда, тень и вода."
        };

        advisories.Add(new WeatherAdvisoryDto(WeatherAdvisoryKind.Clothing, advice));
    }

    private static bool IsRainy(HourlyForecast hour)
    {
        return hour.PrecipMm > 0.05 || hour.ChanceOfRain >= MeaningfulChance;
    }

    private static bool IsSnowy(HourlyForecast hour)
    {
        return hour.ChanceOfSnow >= MeaningfulChance;
    }

    private static string FormatDuration(TimeSpan value)
    {
        int hours = (int)value.TotalHours;
        int minutes = value.Minutes;

        return hours > 0
            ? string.Create(Culture, $"{hours} ч {minutes} мин")
            : string.Create(Culture, $"{minutes} мин");
    }
}
