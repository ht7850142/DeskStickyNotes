using System.Reflection;
using System.Windows.Input;
using DeskStickyNotes.Models;
using DeskStickyNotes.Services;

namespace DeskStickyNotes.ViewModels;

public sealed record ColorChoice(string Code, string DisplayName, string SwatchHex);

public sealed class SettingsViewModel : ObservableObject
{
    private readonly Action<AppSettings> _saveSettings;
    private string _language;
    private string _defaultColor;
    private double _defaultFontSize;
    private bool _startWithWindows;
    private bool _defaultTopmost;
    private bool _keepNotesVisibleAfterShowDesktop;
    private double _autoSaveIntervalSeconds;

    public SettingsViewModel(
        AppSettings settings,
        Action<AppSettings> saveSettings)
    {
        _saveSettings = saveSettings;
        AvailableLanguages = LocalizationService.AvailableLanguages;
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

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? CloseRequested;

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    public IReadOnlyList<ColorChoice> AvailableColors { get; }

    public string AppVersion { get; } =
        typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(SettingsViewModel).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public string Language
    {
        get => _language;
        set => SetProperty(ref _language, LocalizationService.NormalizeLanguage(value));
    }

    public string DefaultColor
    {
        get => _defaultColor;
        set => SetProperty(ref _defaultColor, NotePalette.Normalize(value));
    }

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

    private void Save()
    {
        _saveSettings(new AppSettings
        {
            Language = Language,
            DefaultColor = DefaultColor,
            DefaultFontSize = DefaultFontSize,
            StartWithWindows = StartWithWindows,
            DefaultTopmost = DefaultTopmost,
            KeepNotesVisibleAfterShowDesktop = KeepNotesVisibleAfterShowDesktop,
            AutoSaveIntervalSeconds = (int)Math.Round(AutoSaveIntervalSeconds)
        });

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
