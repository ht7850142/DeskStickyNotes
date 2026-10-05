using System.Globalization;
using System.Threading;
using System.Windows;
using DeskStickyNotes.Models;

namespace DeskStickyNotes.Services;

public static class LocalizationService
{
    public const string AutoLanguage = "Auto";
    public const string English = "en-US";
    public const string SimplifiedChinese = "zh-Hans";

    public static IReadOnlyList<LanguageOption> AvailableLanguages { get; } =
    [
        new(AutoLanguage, "Auto"),
        new(SimplifiedChinese, "简体中文"),
        new(English, "English")
    ];

    public static string CurrentLanguage { get; private set; } = AutoLanguage;

    public static string ActiveCultureName { get; private set; } = English;

    public static event EventHandler? LanguageChanged;

    public static string NormalizeLanguage(string? language)
    {
        return AvailableLanguages.Any(option =>
            string.Equals(option.Code, language, StringComparison.OrdinalIgnoreCase))
            ? language!
            : AutoLanguage;
    }

    public static void ApplyLanguage(string? language)
    {
        CurrentLanguage = NormalizeLanguage(language);
        ActiveCultureName = ResolveCulture(CurrentLanguage);

        var culture = CultureInfo.GetCultureInfo(ActiveCultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        var resources = System.Windows.Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        for (var i = resources.MergedDictionaries.Count - 1; i >= 0; i--)
        {
            var source = resources.MergedDictionaries[i].Source?.OriginalString;
            if (source is not null && source.Contains("Localization/Strings.", StringComparison.OrdinalIgnoreCase))
            {
                resources.MergedDictionaries.RemoveAt(i);
            }
        }

        resources.MergedDictionaries.Insert(0, new ResourceDictionary
        {
            Source = new Uri($"Localization/Strings.{ActiveCultureName}.xaml", UriKind.Relative)
        });
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key)
    {
        return System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
    }

    public static string GetColorName(string color)
    {
        return Get("Color" + color);
    }

    private static string ResolveCulture(string language)
    {
        if (string.Equals(language, AutoLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
                ? SimplifiedChinese
                : English;
        }

        return string.Equals(language, SimplifiedChinese, StringComparison.OrdinalIgnoreCase)
            ? SimplifiedChinese
            : English;
    }
}
