namespace DeskStickyNotes.Models;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 4;

    public string Language { get; set; } = "Auto";

    public string DefaultColor { get; set; } = NotePalette.Yellow;

    public double DefaultFontSize { get; set; } = 15;

    public bool StartWithWindows { get; set; } = true;

    public bool DefaultTopmost { get; set; }

    public bool KeepNotesVisibleAfterShowDesktop { get; set; } = true;

    public int AutoSaveIntervalSeconds { get; set; } = 2;

    public string NewNoteHotkey { get; set; } = HotkeyGesture.DefaultNewNote;
    public string NextNoteHotkey { get; set; } = HotkeyGesture.DefaultNextNote;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            DefaultColor = DefaultColor,
            SchemaVersion = SchemaVersion,
            Language = Language,
            DefaultFontSize = DefaultFontSize,
            StartWithWindows = StartWithWindows,
            DefaultTopmost = DefaultTopmost,
            KeepNotesVisibleAfterShowDesktop = KeepNotesVisibleAfterShowDesktop,
            AutoSaveIntervalSeconds = AutoSaveIntervalSeconds,
            NewNoteHotkey = NewNoteHotkey,
            NextNoteHotkey = NextNoteHotkey
        };
    }
}
