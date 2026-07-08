namespace DeskStickyNotes.Models;

public sealed record NoteColorOption(string Name, string Background, string Header, string Border, string Swatch);

public static class NotePalette
{
    public const string Yellow = "Yellow";
    public const string Green = "Green";
    public const string Blue = "Blue";
    public const string Pink = "Pink";
    public const string White = "White";

    private static readonly NoteColorOption[] Options =
    [
        new(Yellow, "#FFF6C7", "#F8E79D", "#E8D47A", "#FFE27A"),
        new(Green, "#DFF6DD", "#BEE8BC", "#97D395", "#A7E3A2"),
        new(Blue, "#DCEBFF", "#BBD8FF", "#8DB9F0", "#9CC7FF"),
        new(Pink, "#FFE2EC", "#FFC2D6", "#F19AB7", "#FFB3CC"),
        new(White, "#FAFAFA", "#ECEFF3", "#D0D5DD", "#FFFFFF")
    ];

    public static IReadOnlyList<string> Names { get; } = Options.Select(option => option.Name).ToArray();

    public static NoteColorOption Get(string? name)
    {
        return Options.FirstOrDefault(option =>
            string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Options[0];
    }

    public static string Normalize(string? name)
    {
        return Get(name).Name;
    }
}
