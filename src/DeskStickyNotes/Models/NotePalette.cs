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
        new(Yellow, "#FFF8D6", "#F3E7AE", "#D9CA8D", "#E6C65C"),
        new(Green, "#E9F3E5", "#D3E5CA", "#ACC5A2", "#A1C392"),
        new(Blue, "#E9F1FA", "#D2E2F1", "#A6BFD7", "#94B8D7"),
        new(Pink, "#FAEAF0", "#F0D4DF", "#D5AFBE", "#DCA5BB"),
        new(White, "#FAFAF7", "#EAECE6", "#C9CFC4", "#FFFFFF")
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
