namespace DeskStickyNotes.Models;

public sealed class NoteModel
{
    public const double MinWidth = 390;

    public const double MinHeight = 210;

    public const double DefaultWidth = 390;

    public const double DefaultHeight = 300;

    public const double EmptyNoteMaxHeight = 360;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string TextContent { get; set; } = "";

    public string RichContent { get; set; } = "";

    public double X { get; set; } = 100;

    public double Y { get; set; } = 100;

    public double Width { get; set; } = DefaultWidth;

    public double Height { get; set; } = DefaultHeight;

    public string Color { get; set; } = NotePalette.Yellow;

    public bool Topmost { get; set; }

    public bool Visible { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
