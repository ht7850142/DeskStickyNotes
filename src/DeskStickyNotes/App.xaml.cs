using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using DeskStickyNotes.Models;
using DeskStickyNotes.Services;
using DeskStickyNotes.ViewModels;
using DeskStickyNotes.Views;

namespace DeskStickyNotes;

public partial class App : System.Windows.Application
{
    private readonly NoteStorageService _storageService = new();
    private readonly StartupService _startupService = new();
    private readonly ObservableCollection<NoteViewModel> _notes = new();
    private readonly Dictionary<Guid, NoteWindow> _noteWindows = new();
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private bool _isDuplicateInstance;
    private TrayService? _trayService;
    private SettingsWindow? _settingsWindow;
    private DispatcherTimer? _autoSaveTimer;
    private AppSettings _settings = new();

    internal static new App Current => (App)System.Windows.Application.Current;

    internal bool IsExitRequested { get; private set; }

    internal bool KeepNotesVisibleAfterShowDesktop => _settings.KeepNotesVisibleAfterShowDesktop;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, "Local\\DeskStickyNotes.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            _isDuplicateInstance = true;
            Shutdown();
            return;
        }

        _ownsSingleInstanceMutex = true;
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        LoadState();
        LocalizationService.ApplyLanguage(_settings.Language);

        _trayService = new TrayService(
            () => Dispatcher.Invoke(CreateNote),
            () => Dispatcher.Invoke(ShowAllNotes),
            () => Dispatcher.Invoke(HideAllNotes),
            () => Dispatcher.Invoke(ShowSettingsWindow),
            () => Dispatcher.Invoke(ExitApplication));
        _trayService.Show();

        StartAutoSaveTimer();

        foreach (var note in _notes.Where(note => note.Visible).ToList())
        {
            ShowNoteWindow(note, activate: false);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_isDuplicateInstance)
        {
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
            return;
        }

        IsExitRequested = true;
        SaveAll();
        _trayService?.Dispose();
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void LoadState()
    {
        _settings = _storageService.LoadSettings();
        _settings.Language = LocalizationService.NormalizeLanguage(_settings.Language);

        if (_settings.StartWithWindows && !_startupService.IsEnabled())
        {
            _startupService.SetEnabled(true);
        }

        _settings.StartWithWindows = _startupService.IsEnabled();
        _settings.DefaultColor = NotePalette.Normalize(_settings.DefaultColor);
        _settings.DefaultFontSize = Math.Clamp(_settings.DefaultFontSize, 10, 28);
        _settings.AutoSaveIntervalSeconds = Math.Clamp(_settings.AutoSaveIntervalSeconds, 1, 60);

        foreach (var noteModel in _storageService.LoadNotes())
        {
            _notes.Add(CreateViewModel(noteModel));
        }

        if (_notes.Count == 0)
        {
            _notes.Add(CreateViewModel(CreateDefaultNote()));
        }
    }

    private NoteViewModel CreateViewModel(NoteModel model)
    {
        var viewModel = new NoteViewModel(model, _settings.DefaultFontSize);
        viewModel.NewNoteRequested += (_, _) => CreateNote();
        viewModel.HideRequested += (_, _) => HideNote(viewModel);
        viewModel.DeleteRequested += (_, _) => DeleteNote(viewModel);
        return viewModel;
    }

    private NoteModel CreateDefaultNote()
    {
        var workArea = SystemParameters.WorkArea;
        var offset = (_notes.Count % 8) * 24;

        return new NoteModel
        {
            Id = Guid.NewGuid(),
            TextContent = "",
            X = workArea.Left + 96 + offset,
            Y = workArea.Top + 96 + offset,
            Width = NoteModel.DefaultWidth,
            Height = NoteModel.DefaultHeight,
            Color = NotePalette.Normalize(_settings.DefaultColor),
            Topmost = _settings.DefaultTopmost,
            Visible = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private void CreateNote()
    {
        var viewModel = CreateViewModel(CreateDefaultNote());
        _notes.Add(viewModel);
        ShowNoteWindow(viewModel, activate: true);
        SaveAll();
    }

    private void ShowAllNotes()
    {
        foreach (var note in _notes)
        {
            ShowNoteWindow(note, activate: false);
        }

        _noteWindows.Values.LastOrDefault()?.Activate();
        SaveAll();
    }

    private void HideAllNotes()
    {
        foreach (var note in _notes)
        {
            note.Visible = false;
            if (_noteWindows.TryGetValue(note.Id, out var window))
            {
                window.HideFromDesktop();
            }
        }

        SaveAll();
    }

    private void ShowNoteWindow(NoteViewModel note, bool activate)
    {
        if (!_noteWindows.TryGetValue(note.Id, out var window))
        {
            window = new NoteWindow(note);
            window.Closed += (_, _) => _noteWindows.Remove(note.Id);
            _noteWindows[note.Id] = window;
        }

        note.Visible = true;

        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        if (activate)
        {
            window.Activate();
        }
    }

    private void HideNote(NoteViewModel note)
    {
        if (_noteWindows.TryGetValue(note.Id, out var window))
        {
            note.Visible = true;
            window.MinimizeToTaskbar();
        }

        SaveAll();
    }

    private void DeleteNote(NoteViewModel note)
    {
        if (!IsEmptyNote(note))
        {
            var result = _noteWindows.TryGetValue(note.Id, out var owner)
                ? System.Windows.MessageBox.Show(
                    owner,
                    LocalizationService.Get("DeleteConfirmText"),
                    LocalizationService.Get("DeleteConfirmTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question)
                : System.Windows.MessageBox.Show(
                    LocalizationService.Get("DeleteConfirmText"),
                    LocalizationService.Get("DeleteConfirmTitle"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        if (_noteWindows.TryGetValue(note.Id, out var window))
        {
            window.CloseForApplication();
            _noteWindows.Remove(note.Id);
        }

        _notes.Remove(note);
        SaveAll();
    }

    private static bool IsEmptyNote(NoteViewModel note)
    {
        return string.IsNullOrWhiteSpace(note.TextContent);
    }

    private void ShowSettingsWindow()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        var viewModel = new SettingsViewModel(_settings.Clone(), ApplySettings);
        _settingsWindow = new SettingsWindow(viewModel);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ApplySettings(AppSettings settings)
    {
        settings.Language = LocalizationService.NormalizeLanguage(settings.Language);
        settings.DefaultColor = NotePalette.Normalize(settings.DefaultColor);
        settings.DefaultFontSize = Math.Clamp(settings.DefaultFontSize, 10, 28);
        settings.AutoSaveIntervalSeconds = Math.Clamp(settings.AutoSaveIntervalSeconds, 1, 60);

        _settings = settings;
        LocalizationService.ApplyLanguage(_settings.Language);
        _startupService.SetEnabled(_settings.StartWithWindows);

        foreach (var note in _notes)
        {
            note.FontSize = _settings.DefaultFontSize;
        }

        StartAutoSaveTimer();
        _trayService?.RefreshText();
        SaveAll();
    }

    private void StartAutoSaveTimer()
    {
        _autoSaveTimer?.Stop();
        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_settings.AutoSaveIntervalSeconds)
        };
        _autoSaveTimer.Tick += (_, _) => SaveAll();
        _autoSaveTimer.Start();
    }

    private void SaveAll()
    {
        _storageService.SaveSettings(_settings);
        _storageService.SaveNotes(_notes.Select(note => note.ToModel()));
    }

    private void ExitApplication()
    {
        if (IsExitRequested)
        {
            return;
        }

        IsExitRequested = true;
        SaveAll();

        foreach (var window in _noteWindows.Values.ToList())
        {
            window.CloseForApplication();
        }

        _trayService?.Dispose();
        Shutdown();
    }
}
