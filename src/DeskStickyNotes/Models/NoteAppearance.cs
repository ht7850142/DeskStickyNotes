using System.Globalization;

namespace DeskStickyNotes.Models;

public static class NoteTextColorMode
{
    public const string Auto = "Auto";
    public const string Dark = "Dark";
    public const string Light = "Light";

    public static IReadOnlyList<string> Names { get; } = [Auto, Dark, Light];

    public static string Normalize(string? mode)
    {
        return Names.FirstOrDefault(value =>
            string.Equals(value, mode, StringComparison.OrdinalIgnoreCase)) ?? Auto;
    }
}

public static class NoteAppearance
{
    public const double MinBackgroundOpacity = 0.3;
    public const double MaxBackgroundOpacity = 1.0;
    public const string DarkText = "#202124";
    public const string LightText = "#FFFFFF";
    public const string DarkLink = "#155EEF";
    public const string LightLink = "#93C5FD";

    public static double NormalizeOpacity(double opacity)
    {
        return double.IsFinite(opacity)
            ? Math.Clamp(opacity, MinBackgroundOpacity, MaxBackgroundOpacity)
            : MaxBackgroundOpacity;
    }

    public static string WithOpacity(string hex, double opacity)
    {
        var rgb = NormalizeRgbHex(hex);
        var alpha = (byte)Math.Round(NormalizeOpacity(opacity) * byte.MaxValue);
        return $"#{alpha:X2}{rgb}";
    }

    public static string ResolveTextColor(string? mode, string backgroundHex)
    {
        var normalizedMode = NoteTextColorMode.Normalize(mode);
        if (normalizedMode == NoteTextColorMode.Dark)
        {
            return DarkText;
        }

        if (normalizedMode == NoteTextColorMode.Light)
        {
            return LightText;
        }

        var backgroundLuminance = GetRelativeLuminance(backgroundHex);
        var darkContrast = GetContrastRatio(backgroundLuminance, GetRelativeLuminance(DarkText));
        var lightContrast = GetContrastRatio(backgroundLuminance, GetRelativeLuminance(LightText));
        return darkContrast >= lightContrast ? DarkText : LightText;
    }

    public static string ResolveLinkColor(string? mode, string backgroundHex)
    {
        return string.Equals(ResolveTextColor(mode, backgroundHex), LightText, StringComparison.OrdinalIgnoreCase)
            ? LightLink
            : DarkLink;
    }

    private static string NormalizeRgbHex(string hex)
    {
        var value = hex.Trim().TrimStart('#');
        if (value.Length == 8)
        {
            value = value[2..];
        }

        return value.Length == 6 && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            ? value.ToUpperInvariant()
            : "FFFFFF";
    }

    private static double GetRelativeLuminance(string hex)
    {
        var rgb = NormalizeRgbHex(hex);
        var red = ToLinear(byte.Parse(rgb[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0);
        var green = ToLinear(byte.Parse(rgb.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0);
        var blue = ToLinear(byte.Parse(rgb.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0);
        return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
    }

    private static double ToLinear(double channel)
    {
        return channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    private static double GetContrastRatio(double first, double second)
    {
        var lighter = Math.Max(first, second);
        var darker = Math.Min(first, second);
        return (lighter + 0.05) / (darker + 0.05);
    }
}
