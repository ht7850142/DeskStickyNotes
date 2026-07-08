using System.IO;
using System.Text.Json;
using DeskStickyNotes.Models;

namespace DeskStickyNotes.Services;

public sealed class NoteStorageService
{
    private const double LargeEmptyNoteWidth = 640;
    private readonly string _appDirectory;
    private readonly string _notesPath;
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public NoteStorageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _appDirectory = Path.Combine(appData, "DeskStickyNotes");
        _notesPath = Path.Combine(_appDirectory, "notes.json");
        _settingsPath = Path.Combine(_appDirectory, "settings.json");
    }

    public bool HasSettingsFile => File.Exists(_settingsPath);

    public IReadOnlyList<NoteModel> LoadNotes()
    {
        var notes = ReadJson<List<NoteModel>>(_notesPath) ?? [];
        return notes.Select(note => Normalize(note, compactLargeEmptyNote: true)).ToList();
    }

    public AppSettings LoadSettings()
    {
        var settings = ReadJson<AppSettings>(_settingsPath) ?? new AppSettings();
        if (settings.SchemaVersion < 2)
        {
            settings.StartWithWindows = true;
            settings.SchemaVersion = 2;
        }

        if (settings.SchemaVersion < 3)
        {
            settings.StartWithWindows = true;
            settings.SchemaVersion = 3;
        }

        settings.Language = LocalizationService.NormalizeLanguage(settings.Language);
        settings.DefaultColor = NotePalette.Normalize(settings.DefaultColor);
        settings.DefaultFontSize = Math.Clamp(settings.DefaultFontSize, 10, 28);
        settings.AutoSaveIntervalSeconds = Math.Clamp(settings.AutoSaveIntervalSeconds, 1, 60);
        return settings;
    }

    public void SaveNotes(IEnumerable<NoteModel> notes)
    {
        WriteJson(_notesPath, notes.Select(note => Normalize(note, compactLargeEmptyNote: true)).ToList());
    }

    public void SaveSettings(AppSettings settings)
    {
        WriteJson(_settingsPath, settings);
    }

    private T? ReadJson<T>(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return default;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(_appDirectory);

        var tempPath = path + ".tmp";
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    private static NoteModel Normalize(NoteModel note, bool compactLargeEmptyNote)
    {
        note.Id = note.Id == Guid.Empty ? Guid.NewGuid() : note.Id;
        note.TextContent ??= "";
        note.RichContent ??= "";
        note.Width = double.IsFinite(note.Width) ? Math.Clamp(note.Width, NoteModel.MinWidth, 1200) : NoteModel.DefaultWidth;
        note.Height = double.IsFinite(note.Height) ? Math.Clamp(note.Height, NoteModel.MinHeight, 900) : NoteModel.DefaultHeight;
        if (compactLargeEmptyNote && IsEmpty(note) && (note.Width > LargeEmptyNoteWidth || note.Height > NoteModel.EmptyNoteMaxHeight))
        {
            note.Width = NoteModel.DefaultWidth;
            note.Height = NoteModel.DefaultHeight;
        }

        note.X = double.IsFinite(note.X) ? note.X : 100;
        note.Y = double.IsFinite(note.Y) ? note.Y : 100;
        note.Color = NotePalette.Normalize(note.Color);
        note.UpdatedAt = note.UpdatedAt == default ? DateTimeOffset.UtcNow : note.UpdatedAt;
        return note;
    }

    private static bool IsEmpty(NoteModel note)
    {
        return string.IsNullOrWhiteSpace(note.TextContent);
    }
}
