using System.IO;
using System.Text.Json;
using DeskStickyNotes.Models;

namespace DeskStickyNotes.Services;

public sealed class NoteStorageService
{
    private readonly string _appDirectory;
    private readonly string _notesPath;
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public NoteStorageService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskStickyNotes"))
    {
    }

    internal NoteStorageService(string appDirectory)
    {
        _appDirectory = appDirectory;
        _notesPath = Path.Combine(_appDirectory, "notes.json");
        _settingsPath = Path.Combine(_appDirectory, "settings.json");
    }

    public bool HasSettingsFile => File.Exists(_settingsPath);

    public IReadOnlyList<NoteModel> LoadNotes()
    {
        var notes = ReadJson<List<NoteModel>>(_notesPath) ?? [];
        return notes.Select(Normalize).ToList();
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
        WriteJson(_notesPath, notes.Select(Normalize).ToList());
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

    private static NoteModel Normalize(NoteModel note)
    {
        note.Id = note.Id == Guid.Empty ? Guid.NewGuid() : note.Id;
        note.Title = NoteModel.NormalizeTitle(note.Title);
        note.TextContent ??= "";
        note.RichContent ??= "";
        note.TextClips = NormalizeTextClips(note.TextClips);
        note.Width = double.IsFinite(note.Width) ? Math.Max(note.Width, NoteModel.MinWidth) : NoteModel.DefaultWidth;
        note.Height = double.IsFinite(note.Height) ? Math.Max(note.Height, NoteModel.MinHeight) : NoteModel.DefaultHeight;

        note.X = double.IsFinite(note.X) ? note.X : 100;
        note.Y = double.IsFinite(note.Y) ? note.Y : 100;
        note.Color = NotePalette.Normalize(note.Color);
        note.BackgroundOpacity = NoteAppearance.NormalizeOpacity(note.BackgroundOpacity);
        note.TextColorMode = NoteTextColorMode.Normalize(note.TextColorMode);
        note.UpdatedAt = note.UpdatedAt == default ? DateTimeOffset.UtcNow : note.UpdatedAt;
        return note;
    }

    private static List<TextClipModel> NormalizeTextClips(List<TextClipModel>? clips)
    {
        if (clips is null || clips.Count == 0)
        {
            return [];
        }

        var normalized = new List<TextClipModel>(clips.Count);
        var ids = new HashSet<Guid>();
        foreach (var clip in clips)
        {
            if (clip is null)
            {
                continue;
            }

            if (clip.Id == Guid.Empty || !ids.Add(clip.Id))
            {
                continue;
            }
            clip.Content ??= "";
            clip.SourceFileName ??= "";
            clip.FileExtension ??= "";
            clip.OriginalByteLength = Math.Max(0, clip.OriginalByteLength);
            clip.ImportedAt = clip.ImportedAt == default ? DateTimeOffset.UtcNow : clip.ImportedAt;

            if (string.IsNullOrWhiteSpace(clip.Title))
            {
                clip.Title = CreateFallbackClipTitle(clip);
            }

            normalized.Add(clip);
        }

        return normalized;
    }

    private static string CreateFallbackClipTitle(TextClipModel clip)
    {
        if (!string.IsNullOrWhiteSpace(clip.SourceFileName))
        {
            return clip.SourceFileName.Trim();
        }

        var firstLine = clip.Content
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);

        if (string.IsNullOrEmpty(firstLine))
        {
            return "Text clip";
        }

        return firstLine.Length <= 60 ? firstLine : firstLine[..57] + "…";
    }
}
