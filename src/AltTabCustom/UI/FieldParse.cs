using System.Globalization;
using System.Windows.Media;

namespace AltTabCustom.UI;

/// <summary>
/// Tolerant parsing for settings text fields. A bad/typo value falls back to a
/// provided default and clamps to a range, so config can never be corrupted.
/// </summary>
internal static class FieldParse
{
    public static int Int(string text, int fallback, int min, int max)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? Math.Clamp(v, min, max) : fallback;

    public static double Dbl(string text, double fallback, double min, double max)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
            ? Math.Clamp(v, min, max) : fallback;

    public static string Color(string text, string fallback)
    {
        try
        {
            _ = ColorConverter.ConvertFromString(text.Trim());
            return text.Trim();
        }
        catch
        {
            return fallback;
        }
    }
}
