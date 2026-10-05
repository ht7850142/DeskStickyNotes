using System.IO;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DeskStickyNotes;
using DeskStickyNotes.Models;
using DeskStickyNotes.Services;
using DeskStickyNotes.ViewModels;
using DeskStickyNotes.Views;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Forms = System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("All eight resize handles move the requested edges", ResizeEveryEdge),
            ("Minimum size keeps opposite edges anchored and permits reversing a drag", ResizeAtMinimum),
            ("Expansion fits all screen edges and taskbar work areas", FitScreenEdges),
            ("Expansion supports monitors with negative coordinates", FitSecondaryMonitor),
            ("Expansion respects work areas at 100%, 125%, 150%, and 200% DPI", FitHighDpi),
            ("Oversized notes fit smaller monitor work areas", FitOversizedNote),
            ("Empty and populated notes preserve unrestricted custom sizes", PreserveViewModelSize),
            ("Saved empty notes keep custom sizes after reload", PreserveStoredSize),
            ("Invalid dimensions still normalize to usable sizes", NormalizeInvalidSize),
            ("Independent note names persist and old notes load without names", PreserveNoteNames),
            ("Text imports preserve Unicode and Chinese encodings", ImportTextEncodings),
            ("Text imports reject binary, unsupported, and oversized files", RejectInvalidImports),
            ("Text snapshots survive source deletion and storage reload", PreserveTextSnapshot),
            ("Tray lists target individual notes and refresh names and visibility", TrayNoteList),
            ("Window names update, toolbars wrap, and edge icons stay anchored", WindowCollapseRestore)
        };

        var failures = 0;
        foreach (var test in tests)
        {
            try
            {
                test.Run();
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {test.Name}: {exception.GetBaseException().Message}");
            }
        }

        Console.WriteLine($"{tests.Length - failures}/{tests.Length} regression tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void ResizeEveryEdge()
    {
        var start = new Rect(100, 100, 600, 400);
        var minimum = new Size(NoteModel.MinWidth, NoteModel.MinHeight);
        var cases = new (NoteResizeEdge Edge, Rect Expected)[]
        {
            (NoteResizeEdge.Left, new Rect(120, 100, 580, 400)),
            (NoteResizeEdge.Right, new Rect(100, 100, 620, 400)),
            (NoteResizeEdge.Top, new Rect(100, 130, 600, 370)),
            (NoteResizeEdge.Bottom, new Rect(100, 100, 600, 430)),
            (NoteResizeEdge.TopLeft, new Rect(120, 130, 580, 370)),
            (NoteResizeEdge.TopRight, new Rect(100, 130, 620, 370)),
            (NoteResizeEdge.BottomLeft, new Rect(120, 100, 580, 430)),
            (NoteResizeEdge.BottomRight, new Rect(100, 100, 620, 430))
        };
        foreach (var item in cases)
        {
            Equal(item.Expected, NoteWindowGeometry.Resize(start, item.Edge, new Vector(20, 30), minimum));
        }
    }

    private static void ResizeAtMinimum()
    {
        var start = new Rect(-500, 100, 600, 400);
        var minimum = new Size(NoteModel.MinWidth, NoteModel.MinHeight);
        var clamped = NoteWindowGeometry.Resize(start, NoteResizeEdge.TopLeft, new Vector(1000, 1000), minimum);
        Equal(NoteModel.MinWidth, clamped.Width);
        Equal(NoteModel.MinHeight, clamped.Height);
        Equal(start.Right, clamped.Right);
        Equal(start.Bottom, clamped.Bottom);
        Equal(start, NoteWindowGeometry.Resize(start, NoteResizeEdge.TopLeft, new Vector(), minimum));
    }

    private static void FitScreenEdges()
    {
        var workArea = new Rect(0, 40, 1920, 1000);
        foreach (var x in new[] { -30d, 0, 700, 1900, 2050 })
        foreach (var y in new[] { -30d, 40, 350, 1020, 1100 })
        {
            var result = NoteWindowGeometry.FitToWorkArea(new Rect(x, y, 800, 600), workArea);
            Assert(workArea.Contains(result), $"Expanded window lies outside work area: {result}");
            Equal(800, result.Width);
            Equal(600, result.Height);
        }
        Equal(new Rect(1120, 440, 800, 600),
            NoteWindowGeometry.FitToWorkArea(new Rect(1856, 976, 800, 600), workArea));
        Equal(new Rect(500, 200, 800, 600),
            NoteWindowGeometry.FitToWorkArea(new Rect(500, 200, 800, 600), workArea));
    }

    private static void FitSecondaryMonitor()
    {
        foreach (var workArea in new[] { new Rect(-1920, 0, 1920, 1040), new Rect(0, -1080, 1920, 1040) })
        {
            var result = NoteWindowGeometry.FitToWorkArea(
                new Rect(workArea.Right - 64, workArea.Bottom - 64, 900, 700), workArea);
            Assert(workArea.Contains(result), "Expanded window escaped the secondary monitor.");
            Equal(workArea.Right, result.Right);
            Equal(workArea.Bottom, result.Bottom);
        }
    }

    private static void FitHighDpi()
    {
        foreach (var scale in new[] { 1d, 1.25, 1.5, 2 })
        {
            var physicalWorkArea = new Rect(-2560, 0, 2560, 1400);
            var logicalWorkArea = physicalWorkArea;
            logicalWorkArea.Scale(1 / scale, 1 / scale);
            var result = NoteWindowGeometry.FitToWorkArea(
                new Rect(logicalWorkArea.Right - 64, logicalWorkArea.Bottom - 64, 800, 600), logicalWorkArea);
            result.Scale(scale, scale);
            Assert(physicalWorkArea.Contains(result), $"Window escaped physical work area at DPI scale {scale}.");
        }
    }

    private static void FitOversizedNote()
    {
        var workArea = new Rect(1920, 40, 700, 500);
        Equal(workArea, NoteWindowGeometry.FitToWorkArea(new Rect(2556, 476, 1800, 1200), workArea));
    }

    private static void PreserveViewModelSize()
    {
        foreach (var text in new[] { "", "Saved note" })
        {
            var viewModel = new NoteViewModel(new NoteModel { TextContent = text }, 14);
            viewModel.Width = 2400;
            viewModel.Height = 1500;
            Equal(2400, viewModel.Width);
            Equal(1500, viewModel.Height);
        }
    }

    private static void PreserveStoredSize()
    {
        WithTemporaryStorage(storage =>
        {
            var note = new NoteModel { Width = 2400, Height = 1500, X = -900, Y = 40 };
            storage.SaveNotes([note]);
            Equal(2400, note.Width);
            Equal(1500, note.Height);
            var restored = storage.LoadNotes().Single();
            Equal(2400, restored.Width);
            Equal(1500, restored.Height);
            Equal(-900, restored.X);
            Assert(note.Id == restored.Id, "Saving changed the note identity.");
        });
    }

    private static void NormalizeInvalidSize()
    {
        var viewModel = new NoteViewModel(new NoteModel(), 14);
        viewModel.Width = -10;
        viewModel.Height = 1;
        Equal(NoteModel.MinWidth, viewModel.Width);
        Equal(NoteModel.MinHeight, viewModel.Height);
        viewModel.Width = double.NaN;
        viewModel.Height = double.PositiveInfinity;
        Equal(NoteModel.DefaultWidth, viewModel.Width);
        Equal(NoteModel.DefaultHeight, viewModel.Height);
        WithTemporaryStorage(storage =>
        {
            storage.SaveNotes([new NoteModel { Width = double.NaN, Height = double.PositiveInfinity }]);
            var restored = storage.LoadNotes().Single();
            Equal(NoteModel.DefaultWidth, restored.Width);
            Equal(NoteModel.DefaultHeight, restored.Height);
        });
    }

    private static void WindowCollapseRestore()
    {
        // Construct the compiled WPF view without starting the app, tray, or autosave timer.
        var app = new App();
        app.InitializeComponent();
        var viewModel = new NoteViewModel(new NoteModel { Width = 900, Height = 700 }, 14);
        var window = new NoteWindow(viewModel);
        var handles = (Grid)window.FindName("WindowResizeHandles");
        Assert(handles.Children.OfType<Thumb>().Count() == 8, "Missing resize handles.");
        viewModel.Title = "项目提醒";
        Equal("项目提醒", ((TextBlock)window.FindName("NoteTitleText")).Text);
        Assert(window.Title.Contains("项目提醒"), "The window caption did not update after renaming.");
        Assert(((string)((Border)window.FindName("CollapsedIconContent")).ToolTip).Contains("项目提醒"),
            "The collapsed icon does not identify its note.");
        var renameDialog = new RenameNoteWindow(viewModel.Title);
        ((TextBox)renameDialog.FindName("TitleTextBox")).Text = "稍后再改";
        Equal("项目提醒", viewModel.Title);
        Equal("稍后再改", renameDialog.NoteTitle);
        var chrome = (Border)window.Content;
        var toolbar = (WrapPanel)window.FindName("EditorToolbar");
        foreach (var width in new[] { NoteModel.MinWidth, NoteModel.DefaultWidth, 900 })
        {
            chrome.Measure(new Size(width, 300));
            chrome.Arrange(new Rect(0, 0, width, 300));
            chrome.UpdateLayout();
            foreach (var button in toolbar.Children.OfType<Button>())
            {
                var buttonBounds = button.TransformToAncestor(toolbar).TransformBounds(new Rect(new Point(), button.RenderSize));
                Assert(buttonBounds.Left >= 0 && buttonBounds.Right <= toolbar.ActualWidth + 0.5,
                    $"A toolbar button is clipped at note width {width}.");
            }
            if (width == NoteModel.MinWidth) Assert(toolbar.ActualHeight > 32, "The compact toolbar did not wrap.");
        }
        var workArea = SystemParameters.WorkArea;
        window.Left = workArea.Right - 64;
        window.Top = workArea.Bottom - 64;
        Invoke(window, "CollapseToIcon");
        var iconPosition = new Point(window.Left, window.Top);
        Equal(64, window.Width);
        Assert(!handles.IsHitTestVisible, "Collapsed icon still accepts window resize input.");
        Invoke(window, "RestoreFromIcon");
        var expected = NoteWindowGeometry.FitToWorkArea(
            new Rect(workArea.Right - 64, workArea.Bottom - 64, 900, 700), workArea);
        Equal(expected, new Rect(window.Left, window.Top, window.Width, window.Height));
        Assert(handles.IsHitTestVisible, "Restored note is not resizable.");
        Invoke(window, "CollapseToIcon");
        Equal(iconPosition, new Point(window.Left, window.Top));
        Invoke(window, "RestoreFromIcon");
        Equal(expected, new Rect(window.Left, window.Top, window.Width, window.Height));
        Equal(900, viewModel.Width);
        Equal(700, viewModel.Height);
        Assert(app.Resources["TipResizeNote"] is string, "Missing English resize hint.");
        var chineseResources = new ResourceDictionary
        {
            Source = new Uri("/DeskStickyNotes;component/Localization/Strings.zh-Hans.xaml", UriKind.Relative)
        };
        Assert(chineseResources["TipResizeNote"] is string, "Missing Chinese resize hint.");
    }

    private static void PreserveNoteNames()
    {
        WithTemporaryDirectory(directory =>
        {
            var first = new NoteViewModel(new NoteModel(), 14) { Title = "  工作\n计划  " };
            var second = new NoteViewModel(new NoteModel(), 14) { Title = "生活提醒" };
            Equal("工作 计划", first.Title);
            Equal("生活提醒", second.Title);
            var storage = new NoteStorageService(directory);
            storage.SaveNotes([first.ToModel(), second.ToModel()]);
            var restored = storage.LoadNotes();
            Equal("工作 计划", restored[0].Title);
            Equal("生活提醒", restored[1].Title);
            File.WriteAllText(Path.Combine(directory, "notes.json"), "[{\"textContent\":\"old note\"}]");
            Equal("", storage.LoadNotes().Single().Title);
            first.Title = " ";
            Equal("", first.Title);
            first.Title = new string('a', NoteModel.MaximumTitleLength - 1) + "😀";
            Assert(!char.IsHighSurrogate(first.Title[^1]), "The maximum title length split an emoji.");
        });
    }

    private static void ImportTextEncodings()
    {
        WithTemporaryDirectory(directory =>
        {
            var importer = new TextFileImportService();
            const string content = "便签内容\r\nSecond line\t42";
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
                var encodings = new Encoding[]
                {
                    new UTF8Encoding(false), new UTF8Encoding(true),
                    new UnicodeEncoding(false, true), new UnicodeEncoding(true, true),
                    new UTF32Encoding(false, true), new UTF32Encoding(true, true), Encoding.GetEncoding(54936)
                };
                for (var index = 0; index < encodings.Length; index++)
                {
                    var path = Path.Combine(directory, $"encoding-{index}.TXT");
                    File.WriteAllText(path, content, encodings[index]);
                    var result = importer.Import(path);
                    Assert(result.IsSuccess, $"Import failed for {encodings[index].WebName}: {result.Error}");
                    Equal(content, result.Clip!.Content);
                    Equal(new FileInfo(path).Length, result.Clip.OriginalByteLength);
                }
            }
            finally { CultureInfo.CurrentCulture = originalCulture; }
        });
    }

    private static void RejectInvalidImports()
    {
        WithTemporaryDirectory(directory =>
        {
            var importer = new TextFileImportService();
            Equal(TextFileImportError.InvalidPath, importer.Import("").Error);
            Equal(TextFileImportError.FileNotFound, importer.Import(Path.Combine(directory, "missing.txt")).Error);
            var unsupported = Path.Combine(directory, "program.exe");
            File.WriteAllText(unsupported, "plain text");
            Equal(TextFileImportError.UnsupportedFileType, importer.Import(unsupported).Error);
            var binary = Path.Combine(directory, "binary.txt");
            File.WriteAllBytes(binary, [0x01, 0x02, 0x00, 0x03]);
            Equal(TextFileImportError.BinaryContent, importer.Import(binary).Error);
            var invalidUnicode = Path.Combine(directory, "invalid.txt");
            File.WriteAllBytes(invalidUnicode, [0xFF, 0xFE, 0x41]);
            Equal(TextFileImportError.UnsupportedEncoding, importer.Import(invalidUnicode).Error);
            var large = Path.Combine(directory, "large.txt");
            using (var stream = File.Create(large)) stream.SetLength(TextFileImportService.MaximumFileSizeBytes + 1);
            Equal(TextFileImportError.FileTooLarge, importer.Import(large).Error);
        });
    }

    private static void PreserveTextSnapshot()
    {
        WithTemporaryDirectory(directory =>
        {
            var sourcePath = Path.Combine(directory, "source.md");
            File.WriteAllText(sourcePath, "# Snapshot\n便签文本", new UTF8Encoding(false));
            var clip = new TextFileImportService().Import(sourcePath).Clip!;
            File.Delete(sourcePath);
            var note = new NoteModel { TextClips = [clip], Width = 800, Height = 600 };
            Assert(new NoteViewModel(note, 14).HasContent, "A snapshot-only note is considered empty.");
            var storage = new NoteStorageService(directory);
            storage.SaveNotes([note]);
            var restored = storage.LoadNotes().Single().TextClips.Single();
            Equal(clip.Content, restored.Content);
            Equal(clip.Title, restored.Title);
            Equal(clip.OriginalByteLength, restored.OriginalByteLength);
            Equal(clip.Id, restored.Id);
        });
    }

    private static void TrayNoteList()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var notes = new List<TrayNote> { new(firstId, "工作 & 计划", true), new(secondId, "生活提醒", false) };
        var requests = new List<(Guid Id, bool Visible)>();
        using var tray = new TrayService(() => { }, () => { }, () => { }, () => { }, () => { },
            () => notes, (id, visible) => requests.Add((id, visible)));
        using var menu = tray.CreateContextMenu();
        var list = menu.Items.OfType<Forms.ToolStripMenuItem>().Single(item => item.DropDownItems.Count > 0);
        var first = (Forms.ToolStripMenuItem)list.DropDownItems[0];
        var second = (Forms.ToolStripMenuItem)list.DropDownItems[1];
        Assert(first.Checked && !second.Checked, "The tray does not distinguish visible and hidden notes.");
        Equal("工作 && 计划", first.Text);
        first.DropDownItems[1].PerformClick();
        second.DropDownItems[0].PerformClick();
        Equal((firstId, false), requests[0]);
        Equal((secondId, true), requests[1]);
        notes[0] = notes[0] with { Title = "改名后", Visible = false };
        tray.RefreshNoteList();
        first = (Forms.ToolStripMenuItem)list.DropDownItems[0];
        Equal("改名后", first.Text);
        Assert(!first.Checked, "Tray visibility did not refresh.");
        notes.Clear();
        tray.RefreshNoteList();
        Equal(1, list.DropDownItems.Count);
        Assert(!list.DropDownItems[0].Enabled, "An empty note list should not contain an actionable item.");
        WithTemporaryStorage(storage =>
        {
            storage.SaveNotes([new NoteModel { Title = "隐藏的便签", Visible = false }]);
            Assert(!storage.LoadNotes().Single().Visible, "A hidden note reappears after reloading storage.");
        });
    }

    private static void Invoke(NoteWindow window, string method) =>
        typeof(NoteWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);

    private static void WithTemporaryStorage(Action<NoteStorageService> test)
        => WithTemporaryDirectory(directory => test(new NoteStorageService(directory)));

    private static void WithTemporaryDirectory(Action<string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DeskStickyNotes.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            test(directory);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void Equal<T>(T expected, T actual) =>
        Assert(EqualityComparer<T>.Default.Equals(expected, actual), $"Expected {expected}; got {actual}.");

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
