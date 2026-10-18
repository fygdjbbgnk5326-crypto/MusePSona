using System.Globalization;

namespace MuseDashChart;

public static class ParseUtil
{
    public static bool TryFloat(string text, out float value)
        => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    public static bool TryInt(string text, out int value)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryDecimal(string text, out decimal value)
        => decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    public static string Dec(decimal value)
        => value.ToString("0.########", CultureInfo.InvariantCulture);

    public static string Flt(float value)
        => value.ToString("0.######", CultureInfo.InvariantCulture);
}

