namespace DeskStickyNotes.Models;

public sealed class TextClipModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = "";

    public string Content { get; set; } = "";

    public string SourceFileName { get; set; } = "";

    public string FileExtension { get; set; } = "";

    public long OriginalByteLength { get; set; }

    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
}
