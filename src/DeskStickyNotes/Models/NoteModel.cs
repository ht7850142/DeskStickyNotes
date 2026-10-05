namespace DeskStickyNotes.Models;

public sealed class NoteModel
{
    public const double MinWidth = 300;

    public const double MinHeight = 180;

    public const double DefaultWidth = 390;

    public const double DefaultHeight = 300;

    public const int MaximumTitleLength = 80;

    public const long MaximumTextClipBytes = 8 * 1024 * 1024;

    public const int MaximumTextClipCount = 200;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = "";

    public static string NormalizeTitle(string? title)
    {
        var normalized = string.Join(" ", (title ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length <= MaximumTitleLength) return normalized;
        var length = char.IsHighSurrogate(normalized[MaximumTitleLength - 1]) ? MaximumTitleLength - 1 : MaximumTitleLength;
        return normalized[..length];
    }

    public string TextContent { get; set; } = "";

    public string RichContent { get; set; } = "";

    public List<TextClipModel> TextClips { get; set; } = [];

    public double X { get; set; } = 100;

    public double Y { get; set; } = 100;

    public double Width { get; set; } = DefaultWidth;

    public double Height { get; set; } = DefaultHeight;

    public string Color { get; set; } = NotePalette.Yellow;

    public double BackgroundOpacity { get; set; } = NoteAppearance.MaxBackgroundOpacity;

    public string TextColorMode { get; set; } = NoteTextColorMode.Auto;

    public bool Topmost { get; set; }

    public bool Visible { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
