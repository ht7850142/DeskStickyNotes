using System.Reflection;
using System.Windows.Input;
using DeskStickyNotes.Models;
using DeskStickyNotes.Services;

namespace DeskStickyNotes.ViewModels;

public sealed record ColorChoice(string Code, string DisplayName, string SwatchHex);

public sealed class SettingsViewModel : ObservableObject
{
    private readonly Action<AppSettings> _saveSettings;
    private readonly Func<AppSettings, string?>? _validateHotkeys;
    private string _language;
    private string _defaultColor;
    private double _defaultFontSize;
    private bool _startWithWindows;
    private bool _defaultTopmost;
    private bool _keepNotesVisibleAfterShowDesktop;
    private double _autoSaveIntervalSeconds;
    private string _newNoteHotkey;
    private string _nextNoteHotkey;
    private string _validationError = "";

    public SettingsViewModel(
        AppSettings settings,
        Action<AppSettings> saveSettings,
        Func<AppSettings, string?>? validateHotkeys = null)
    {
        _saveSettings = saveSettings;
        _validateHotkeys = validateHotkeys;
        AvailableLanguages = LocalizationService.AvailableLanguages.Select(option => option.Code == LocalizationService.AutoLanguage
            ? new LanguageOption(option.Code, LocalizationService.Get("LanguageAuto")) : option).ToArray();
        AvailableColors = NotePalette.Names
            .Select(color => new ColorChoice(
                color,
                LocalizationService.GetColorName(color),
                NotePalette.Get(color).Swatch))
            .ToArray();
        _language = LocalizationService.NormalizeLanguage(settings.Language);
        _defaultColor = NotePalette.Normalize(settings.DefaultColor);
        _defaultFontSize = settings.DefaultFontSize;
        _startWithWindows = settings.StartWithWindows;
        _defaultTopmost = settings.DefaultTopmost;
        _keepNotesVisibleAfterShowDesktop = settings.KeepNotesVisibleAfterShowDesktop;
        _autoSaveIntervalSeconds = settings.AutoSaveIntervalSeconds;
        _newNoteHotkey = settings.NewNoteHotkey ?? HotkeyGesture.DefaultNewNote;
        _nextNoteHotkey = settings.NextNoteHotkey ?? HotkeyGesture.DefaultNextNote;

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
        ClearNewNoteHotkeyCommand = new RelayCommand(() => NewNoteHotkey = "");
        ClearNextNoteHotkeyCommand = new RelayCommand(() => NextNoteHotkey = "");
        ResetHotkeysCommand = new RelayCommand(() => { NewNoteHotkey = HotkeyGesture.DefaultNewNote; NextNoteHotkey = HotkeyGesture.DefaultNextNote; });
    }

    public event EventHandler? CloseRequested;

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    public IReadOnlyList<ColorChoice> AvailableColors { get; }

    public string AppVersion { get; } = (
        typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(SettingsViewModel).Assembly.GetName().Version?.ToString()
        ?? "0.0.0").Split('+')[0];

    public string Language
    {
        get => _language;
        set => SetProperty(ref _language, LocalizationService.NormalizeLanguage(value));
    }

    public string DefaultColor
    {
        get => _defaultColor;
        set
        {
            if (!SetProperty(ref _defaultColor, NotePalette.Normalize(value))) return;
            OnPropertyChanged(nameof(PreviewBackground));
            OnPropertyChanged(nameof(PreviewHeader));
        }
    }

    public string PreviewBackground => NotePalette.Get(DefaultColor).Background;
    public string PreviewHeader => NotePalette.Get(DefaultColor).Header;

    public double DefaultFontSize
    {
        get => _defaultFontSize;
        set => SetProperty(ref _defaultFontSize, Math.Clamp(value, 10, 28));
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool DefaultTopmost
    {
        get => _defaultTopmost;
        set => SetProperty(ref _defaultTopmost, value);
    }

    public bool KeepNotesVisibleAfterShowDesktop
    {
        get => _keepNotesVisibleAfterShowDesktop;
        set => SetProperty(ref _keepNotesVisibleAfterShowDesktop, value);
    }

    public double AutoSaveIntervalSeconds
    {
        get => _autoSaveIntervalSeconds;
        set => SetProperty(ref _autoSaveIntervalSeconds, Math.Clamp(value, 1, 60));
    }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand ClearNewNoteHotkeyCommand { get; }
    public ICommand ClearNextNoteHotkeyCommand { get; }
    public ICommand ResetHotkeysCommand { get; }

    public string NewNoteHotkey
    {
        get => _newNoteHotkey;
        set { if (SetProperty(ref _newNoteHotkey, value)) SetHotkeyError(""); }
    }
    public string NextNoteHotkey
    {
        get => _nextNoteHotkey;
        set { if (SetProperty(ref _nextNoteHotkey, value)) SetHotkeyError(""); }
    }
    public string ValidationError => _validationError;
    public bool HasValidationError => !string.IsNullOrEmpty(_validationError);
    internal void SetHotkeyError(string message)
    {
        if (!SetProperty(ref _validationError, message, nameof(ValidationError))) return;
        OnPropertyChanged(nameof(HasValidationError));
    }

    private void Save()
    {
        var settings = new AppSettings
        {
            Language = Language,
            DefaultColor = DefaultColor,
            DefaultFontSize = DefaultFontSize,
            StartWithWindows = StartWithWindows,
            DefaultTopmost = DefaultTopmost,
            KeepNotesVisibleAfterShowDesktop = KeepNotesVisibleAfterShowDesktop,
            AutoSaveIntervalSeconds = (int)Math.Round(AutoSaveIntervalSeconds),
            NewNoteHotkey = NewNoteHotkey,
            NextNoteHotkey = NextNoteHotkey
        };
        if (_validateHotkeys?.Invoke(settings) is { } error)
        {
            SetHotkeyError(error);
            return;
        }
        _saveSettings(settings);

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
