using System.Globalization;
using System.Windows;
using System.Windows.Input;
using DeskStickyNotes.Models;
using DeskStickyNotes.Services;

namespace DeskStickyNotes.Views;

public partial class TextClipPreviewWindow : Window
{
    private readonly TextClipModel _clip;
    private string _copyButtonText = "";

    public TextClipPreviewWindow(TextClipModel clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        InitializeComponent();

        _clip = clip;
        ContentTextBox.Text = clip.Content;
        if (_clip.Content.Length > 500_000)
        {
            WrapTextCheckBox.IsEnabled = false;
        }

        UpdateLocalizedText();
        LocalizationService.LanguageChanged += LocalizationService_LanguageChanged;
        Closed += (_, _) => LocalizationService.LanguageChanged -= LocalizationService_LanguageChanged;

        Loaded += (_, _) => ContentTextBox.Focus();
    }

    private void LocalizationService_LanguageChanged(object? sender, EventArgs e)
    {
        UpdateLocalizedText();
    }

    private void UpdateLocalizedText()
    {
        _copyButtonText = LocalizationService.Get("TextClipPreviewCopy");
        Title = $"{_clip.Title} — {LocalizationService.Get("TextClipPreviewTitle")}";
        TitleTextBlock.Text = _clip.Title;
        MetadataTextBlock.Text = CreateMetadata(_clip);
        CopyButton.Content = _copyButtonText;
        var wrapHint = WrapTextCheckBox.IsEnabled
            ? null
            : LocalizationService.Get("TextClipPreviewWrapLargeHint");
        WrapTextCheckBox.ToolTip = wrapHint;
        WrapOptionPanel.ToolTip = wrapHint;
        WrapOptionPanel.Cursor = WrapTextCheckBox.IsEnabled
            ? System.Windows.Input.Cursors.Hand
            : System.Windows.Input.Cursors.Arrow;
        WrapOptionPanel.Opacity = WrapTextCheckBox.IsEnabled ? 1 : 0.58;
        System.Windows.Automation.AutomationProperties.SetHelpText(WrapOptionPanel, wrapHint ?? "");
    }

    private static string CreateMetadata(TextClipModel clip)
    {
        var kind = string.IsNullOrWhiteSpace(clip.SourceFileName)
            ? LocalizationService.Get("TextClipSourcePastedText")
            : clip.SourceFileName;
        var lineCount = CountLines(clip.Content);
        var size = FormatSize(clip.OriginalByteLength > 0
            ? clip.OriginalByteLength
            : System.Text.Encoding.UTF8.GetByteCount(clip.Content));

        return string.Format(
            CultureInfo.CurrentCulture,
            LocalizationService.Get(lineCount == 1
                ? "TextClipPreviewMetadataOneLine"
                : "TextClipPreviewMetadataManyLines"),
            kind,
            lineCount,
            size);
    }

    private static int CountLines(string content)
    {
        if (content.Length == 0)
        {
            return 0;
        }

        var lines = 1;
        foreach (var character in content)
        {
            if (character == '\n')
            {
                lines++;
            }
        }

        return lines;
    }

    private static string FormatSize(long byteCount)
    {
        if (byteCount < 1024)
        {
            return $"{byteCount} B";
        }

        if (byteCount < 1024 * 1024)
        {
            return $"{byteCount / 1024d:0.#} KB";
        }

        return $"{byteCount / (1024d * 1024d):0.##} MB";
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(ContentTextBox.Text);
            CopyButton.Content = LocalizationService.Get("TextClipPreviewCopied");

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                CopyButton.Content = _copyButtonText;
            };
            timer.Start();
        }
        catch
        {
            System.Windows.MessageBox.Show(
                this,
                LocalizationService.Get("TextClipClipboardError"),
                LocalizationService.Get("TextClipPreviewTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void WrapTextCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        ContentTextBox.TextWrapping = WrapTextCheckBox.IsChecked == true
            ? TextWrapping.Wrap
            : TextWrapping.NoWrap;
        ContentTextBox.HorizontalScrollBarVisibility = WrapTextCheckBox.IsChecked == true
            ? System.Windows.Controls.ScrollBarVisibility.Disabled
            : System.Windows.Controls.ScrollBarVisibility.Auto;
    }

    private void WrapOption_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && FindAncestor<System.Windows.Controls.CheckBox>(source) is not null)
        {
            return;
        }

        if (WrapTextCheckBox.IsEnabled)
        {
            WrapTextCheckBox.IsChecked = WrapTextCheckBox.IsChecked != true;
            WrapTextCheckBox.Focus();
            e.Handled = true;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            var logicalParent = LogicalTreeHelper.GetParent(source);
            source = logicalParent ?? source switch
            {
                System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                    => System.Windows.Media.VisualTreeHelper.GetParent(source),
                _ => null
            };
        }

        return null;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
