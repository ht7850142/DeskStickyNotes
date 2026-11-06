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
    public const string TraditionalChinese = "zh-Hant";
    private static readonly CultureInfo SystemUICulture = CultureInfo.CurrentUICulture;

    public static IReadOnlyList<LanguageOption> AvailableLanguages { get; } =
    [
        new(AutoLanguage, "Auto"),
        new(SimplifiedChinese, "简体中文"),
        new(TraditionalChinese, "繁體中文"),
        new(English, "English"),
        new("fr-FR", "Français"),
        new("es-ES", "Español"),
        new("pt-PT", "Português"),
        new("ja-JP", "日本語")
    ];

    public static string CurrentLanguage { get; private set; } = AutoLanguage;

    public static string ActiveCultureName { get; private set; } = English;
    public static string UiFontName => ActiveCultureName switch
    {
        "ja-JP" => "Yu Gothic UI", TraditionalChinese => "Microsoft JhengHei UI",
        SimplifiedChinese => "Microsoft YaHei UI", _ => "Segoe UI"
    };

    public static event EventHandler? LanguageChanged;

    public static string NormalizeLanguage(string? language)
    {
        return AvailableLanguages.FirstOrDefault(option =>
            string.Equals(option.Code, language, StringComparison.OrdinalIgnoreCase))?.Code ?? AutoLanguage;
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
        resources["UiFontFamily"] = new System.Windows.Media.FontFamily($"{UiFontName}, Segoe UI");
        resources["UiLanguage"] = System.Windows.Markup.XmlLanguage.GetLanguage(ActiveCultureName);

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
            Source = new Uri($"/DeskStickyNotes;component/Localization/Strings.{ActiveCultureName}.xaml", UriKind.Relative)
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

    internal static string ResolveCulture(string language, CultureInfo? systemCulture = null)
    {
        var normalized = NormalizeLanguage(language);
        if (normalized != AutoLanguage) return normalized;
        var culture = systemCulture ?? SystemUICulture;
        if (culture.TwoLetterISOLanguageName == "zh")
        {
            var name = culture.Name.ToUpperInvariant();
            return name.Contains("HANT", StringComparison.Ordinal) || name is "ZH-TW" or "ZH-HK" or "ZH-MO"
                ? TraditionalChinese : SimplifiedChinese;
        }
        return culture.TwoLetterISOLanguageName switch
        {
            "fr" => "fr-FR", "es" => "es-ES", "pt" => "pt-PT", "ja" => "ja-JP", _ => English
        };
    }
}
