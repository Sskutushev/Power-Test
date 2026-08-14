namespace Weather.Web.Formatting;

/// <summary>
/// Maps a WeatherAPI condition code onto a small set of visual moods. The animated background reacts to
/// the real forecast instead of playing a decorative loop, and the set is intentionally tiny: five moods
/// are enough to read at a glance, and every unknown code degrades to a neutral one.
/// </summary>
public static class ConditionMood
{
    /// <summary>Returns the mood token used by the stylesheet.</summary>
    public static string FromCode(int code)
    {
        return code switch
        {
            1000 => "clear",
            1003 or 1006 or 1009 or 1030 or 1135 or 1147 => "cloudy",
            1063 or 1069 or 1072 or 1150 or 1153 or 1168 or 1171 or 1180 or 1183 or 1186 or 1189
                or 1192 or 1195 or 1198 or 1201 or 1240 or 1243 or 1246 => "rain",
            1066 or 1114 or 1117 or 1204 or 1207 or 1210 or 1213 or 1216 or 1219 or 1222 or 1225
                or 1237 or 1249 or 1252 or 1255 or 1258 or 1261 or 1264 or 1279 or 1282 => "snow",
            1087 or 1273 or 1276 => "storm",
            _ => "neutral"
        };
    }
}
