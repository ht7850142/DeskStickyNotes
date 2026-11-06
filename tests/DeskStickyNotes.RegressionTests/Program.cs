using System.IO;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using System.Text.RegularExpressions;
using DeskStickyNotes;
using DeskStickyNotes.Models;
using DeskStickyNotes.Services;
using DeskStickyNotes.ViewModels;
using DeskStickyNotes.Views;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using RichTextBox = System.Windows.Controls.RichTextBox;
using ListBox = System.Windows.Controls.ListBox;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Forms = System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--render-preview") return RenderPreview(args[1]);
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
            ("Desktop entry restores a group once without idle z-order churn", RecoverDesktopOnce),
            ("Recovery preserves the most recently activated note and coalesces shell events", RecoverInActivationOrder),
            ("Suppressed notes recover together with bounded retries", BoundDesktopRecovery),
            ("Hidden, taskbar-minimized, and opted-out notes stay excluded", ExcludeIntentionalHiding),
            ("Cloaking on another desktop does not trigger recovery", IgnoreBackgroundCloaking),
            ("Settings previews remain drafts until Save and Cancel preserves existing settings", SettingsDrafts),
            ("Global shortcuts parse and reject typing-only or malformed combinations", ParseHotkeys),
            ("Shortcut updates roll back conflicts and support swaps without losing working keys", HotkeyTransactions),
            ("Shortcut capture suspends dispatch, supports disabling, and resumes safely", HotkeyCapture),
            ("Shortcut settings persist and old settings retain usable defaults", PersistHotkeys),
            ("Shortcut validation keeps settings open without saving a failed draft", RejectHotkeyDraft),
            ("Language selection resolves regional variants and canonical codes", ResolveLanguages),
            ("Window names, compact layout, editor feedback, settings layout, pin preference, and native stacking", WindowCollapseRestore)
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
                Console.Error.WriteLine($"FAIL {test.Name}: {exception}");
            }
        }

        Console.WriteLine($"{tests.Length - failures}/{tests.Length} regression tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void ParseHotkeys()
    {
        foreach (var text in new[] { HotkeyGesture.DefaultNewNote, HotkeyGesture.DefaultNextNote, "Alt+Shift+F8", "Ctrl+Shift+PageUp", "Win+Alt+Enter" })
        {
            Assert(HotkeyGesture.TryParse(text, out var gesture), $"Valid shortcut rejected: {text}");
            Assert(HotkeyGesture.TryParse(gesture.ToString(), out var restored) && restored == gesture, "Shortcut did not round-trip.");
        }
        foreach (var text in new[] { "N", "Shift+N", "Ctrl", "Ctrl+Ctrl+N", "Ctrl++N", "Ctrl+None", "Ctrl+LeftCtrl", "Alt+System", "Ctrl+99999" })
            Assert(!HotkeyGesture.TryParse(text, out _), $"Invalid shortcut accepted: {text}");
        Assert(HotkeyGesture.TryParse(" control + alt + n ", out var canonical), "Alias normalization failed.");
        Equal(HotkeyGesture.DefaultNewNote, canonical.ToString());
    }

    private static void HotkeyTransactions()
    {
        var native = new FakeHotkeyRegistrar();
        using var manager = new HotkeyRegistrationManager(native);
        var calls = new List<HotkeyAction>();
        manager.Invoked += calls.Add;
        Assert(manager.TryApply("Ctrl+Alt+N", "Ctrl+Alt+PageDown", out _), "Initial shortcuts failed.");
        var original = manager.Entries.ToArray();
        native.Blocked.Add(ParseGesture("Ctrl+Shift+X"));
        Assert(!manager.TryApply("Ctrl+Shift+M", "Ctrl+Shift+X", out var failure), "A conflicting update was accepted.");
        Equal(HotkeyFailureReason.Unavailable, failure!.Reason);
        Equal(2, native.Active.Count);
        Assert(original.SequenceEqual(manager.Entries), "A failed update changed the working shortcut set.");
        Assert(!native.Active.Values.Contains(ParseGesture("Ctrl+Shift+M")), "A failed update leaked a reservation.");
        manager.Dispatch(original[0].Id);
        Equal(HotkeyAction.NewNote, calls.Single());
        var registrations = native.RegisterCount;
        Assert(manager.TryApply("Ctrl+Alt+PageDown", "Ctrl+Alt+N", out _), "Swapping shortcuts failed.");
        Equal(registrations, native.RegisterCount);
        manager.Dispatch(original[0].Id);
        Equal(HotkeyAction.NextNote, calls.Last());
        Assert(!manager.TryApply("Ctrl+Alt+N", "control+alt+n", out failure), "Duplicate shortcuts were accepted.");
        Equal(HotkeyFailureReason.Duplicate, failure!.Reason);
        Equal(2, native.Active.Count);
        var startupNative = new FakeHotkeyRegistrar();
        startupNative.Blocked.Add(ParseGesture("Ctrl+Alt+N"));
        using var startup = new HotkeyRegistrationManager(startupNative);
        Assert(!startup.TryInitialize("Ctrl+Alt+N", "Ctrl+Alt+PageDown", out _), "Startup conflict was not reported.");
        Equal(1, startupNative.Active.Count);
        Equal(HotkeyAction.NextNote, startup.Entries.Single().Action);
    }

    private static void HotkeyCapture()
    {
        var native = new FakeHotkeyRegistrar();
        using var manager = new HotkeyRegistrationManager(native);
        var calls = 0;
        manager.Invoked += _ => calls++;
        manager.TryApply("Ctrl+Alt+N", "Ctrl+Alt+PageDown", out _);
        var oldId = manager.Entries[0].Id;
        manager.Suspend(); manager.Suspend();
        Equal(0, native.Active.Count);
        manager.Dispatch(oldId);
        Equal(0, calls);
        Assert(manager.TryApply("Ctrl+Shift+N", "", out _), "A suspended update failed.");
        Equal(0, native.Active.Count);
        Assert(manager.Resume(out _), "Nested suspension did not resume.");
        Equal(0, native.Active.Count);
        Assert(manager.Resume(out _), "Final suspension did not resume.");
        Equal(1, native.Active.Count);
        manager.Dispatch(manager.Entries.Single().Id);
        Equal(1, calls);
        manager.Suspend();
        native.Blocked.Add(ParseGesture("Ctrl+Shift+N"));
        Assert(!manager.Resume(out var failure), "A newly occupied key was silently accepted on resume.");
        Equal(HotkeyFailureReason.Unavailable, failure!.Reason);
        Equal(0, native.Active.Count);
        Assert(manager.TryApply("", "", out _), "Disabling all shortcuts failed.");
        Equal(0, manager.Entries.Count);
    }

    private static HotkeyGesture ParseGesture(string value)
    {
        Assert(HotkeyGesture.TryParse(value, out var result), $"Bad test shortcut: {value}");
        return result;
    }

    private sealed class FakeHotkeyRegistrar : IHotkeyRegistrar
    {
        public Dictionary<int, HotkeyGesture> Active { get; } = [];
        public HashSet<HotkeyGesture> Blocked { get; } = [];
        public int RegisterCount { get; private set; }
        public bool Register(int id, HotkeyGesture gesture)
        {
            RegisterCount++;
            if (Blocked.Contains(gesture) || Active.Values.Contains(gesture)) return false;
            Active.Add(id, gesture);
            return true;
        }
        public void Unregister(int id) => Active.Remove(id);
    }

    private static void PersistHotkeys() => WithTemporaryDirectory(directory =>
    {
        var storage = new NoteStorageService(directory);
        var settings = new AppSettings { Language = "fr-FR", NewNoteHotkey = "Ctrl+Shift+N", NextNoteHotkey = "" };
        storage.SaveSettings(settings);
        var restored = storage.LoadSettings();
        Equal("Ctrl+Shift+N", restored.NewNoteHotkey);
        Equal("", restored.NextNoteHotkey);
        Equal("fr-FR", restored.Language);
        Equal(restored.NewNoteHotkey, restored.Clone().NewNoteHotkey);
        Equal(restored.NextNoteHotkey, restored.Clone().NextNoteHotkey);
        File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"schemaVersion\":3,\"language\":\"en-US\"}");
        restored = storage.LoadSettings();
        Equal(HotkeyGesture.DefaultNewNote, restored.NewNoteHotkey);
        Equal(HotkeyGesture.DefaultNextNote, restored.NextNoteHotkey);
        Equal(4, restored.SchemaVersion);
    });

    private static void RejectHotkeyDraft()
    {
        var saved = 0;
        var closed = 0;
        var reject = true;
        var model = new SettingsViewModel(new AppSettings(), _ => saved++, _ => reject ? "conflict" : null);
        model.CloseRequested += (_, _) => closed++;
        model.SaveCommand.Execute(null);
        Equal(0, saved); Equal(0, closed);
        Assert(model.HasValidationError && model.ValidationError == "conflict", "Conflict feedback is missing.");
        model.ClearNextNoteHotkeyCommand.Execute(null);
        Assert(!model.HasValidationError && model.NextNoteHotkey == "", "Editing did not clear the prior error.");
        reject = false;
        model.SaveCommand.Execute(null);
        Equal(1, saved); Equal(1, closed);
    }

    private static void ResolveLanguages()
    {
        foreach (var (system, expected) in new[] { ("fr-CA", "fr-FR"), ("es-MX", "es-ES"), ("pt-BR", "pt-PT"), ("ja-JP", "ja-JP"),
            ("zh-TW", "zh-Hant"), ("zh-HK", "zh-Hant"), ("zh-MO", "zh-Hant"), ("zh-SG", "zh-Hans"), ("zh-CN", "zh-Hans"), ("de-DE", "en-US") })
            Equal(expected, LocalizationService.ResolveCulture("Auto", CultureInfo.GetCultureInfo(system)));
        Equal("fr-FR", LocalizationService.NormalizeLanguage("FR-fr"));
        Equal("Auto", LocalizationService.NormalizeLanguage("unsupported"));
    }

    private static void RecoverDesktopOnce()
    {
        var calls = new List<string>();
        using var service = new DesktopNoteRecoveryService(() => false);
        service.Register(new RecoveryNote("A", calls));
        service.Register(new RecoveryNote("B", calls));
        service.CheckWindows(false);
        Equal(0, calls.Count);
        service.CheckWindows(true);
        Equal("A,B", string.Join(',', calls));
        for (var tick = 0; tick < 240; tick++) service.CheckWindows(true);
        Equal(2, calls.Count);
        service.CheckWindows(false);
        service.CheckWindows(true);
        Equal("A,B,A,B", string.Join(',', calls));
    }

    private static void RecoverInActivationOrder()
    {
        var calls = new List<string>();
        using var service = new DesktopNoteRecoveryService(() => false);
        var a = new RecoveryNote("A", calls);
        var b = new RecoveryNote("B", calls);
        service.Register(a);
        service.Register(b);
        service.Register(a);
        service.NoteActivated(a);
        a.OnRecover = () => { service.RequestRecovery(); service.NoteActivated(b); };
        for (var notification = 0; notification < 100; notification++) service.RequestRecovery();
        service.CheckWindows(true);
        Equal("B,A", string.Join(',', calls));
        service.CheckWindows(true);
        Equal(2, calls.Count);
        service.Unregister(a);
        service.Unregister(b);
        service.CheckWindows(false);
        service.CheckWindows(true);
        Equal(2, calls.Count);
    }

    private static void BoundDesktopRecovery()
    {
        var calls = new List<string>();
        using var service = new DesktopNoteRecoveryService(() => false);
        var a = new RecoveryNote("A", calls) { IsSuppressedByShell = true };
        var b = new RecoveryNote("B", calls) { IsSuppressedByShell = true };
        service.Register(a);
        service.Register(b);
        for (var tick = 0; tick < 240; tick++)
        {
            service.RequestRecovery();
            service.CheckWindows(true);
        }
        Equal("A,B,A,B,A,B", string.Join(',', calls));
        Equal(3, a.VisibilityRestores);
        // A successful recovery resets the budget for a later shell operation.
        a.IsSuppressedByShell = b.IsSuppressedByShell = false;
        service.CheckWindows(true);
        a.IsSuppressedByShell = true;
        a.OnRecover = () => a.IsSuppressedByShell = false;
        service.CheckWindows(true);
        Equal(4, a.VisibilityRestores);
        Equal("A,B", string.Join(',', calls.TakeLast(2)));
        service.CheckWindows(true);
        Equal(8, calls.Count);
    }

    private static void ExcludeIntentionalHiding()
    {
        var calls = new List<string>();
        using var service = new DesktopNoteRecoveryService(() => false);
        var hidden = new RecoveryNote("hidden", calls) { CanRecoverAfterShowDesktop = false, IsSuppressedByShell = true };
        service.Register(hidden);
        service.RequestRecovery();
        service.CheckWindows(true);
        Equal(0, calls.Count);
        hidden.CanRecoverAfterShowDesktop = true;
        service.RequestRecovery();
        service.CheckWindows(true);
        Equal("hidden", calls.Single());
    }

    private static void IgnoreBackgroundCloaking()
    {
        var calls = new List<string>();
        using var service = new DesktopNoteRecoveryService(() => false);
        var note = new RecoveryNote("A", calls) { IsSuppressedByShell = true };
        service.Register(note);
        for (var tick = 0; tick < 240; tick++) service.CheckWindows(false);
        Equal(0, calls.Count);
        service.RequestRecovery();
        service.CheckWindows(false);
        Equal(1, calls.Count);
    }

    private sealed class RecoveryNote(string name, List<string> calls) : IDesktopRecoverableNote
    {
        public bool CanRecoverAfterShowDesktop { get; set; } = true;
        public bool IsSuppressedByShell { get; set; }
        public int VisibilityRestores { get; private set; }
        public Action? OnRecover { get; set; }
        public void RecoverAfterShowDesktop(bool restoreVisibility)
        {
            calls.Add(name);
            if (restoreVisibility) VisibilityRestores++;
            OnRecover?.Invoke();
        }
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
        var app = new RegressionApp();
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
            var buttons = Descendants<Button>(toolbar).ToList();
            Equal(13, buttons.Count);
            foreach (var button in buttons)
            {
                var buttonBounds = button.TransformToAncestor(toolbar).TransformBounds(new Rect(new Point(), button.RenderSize));
                Assert(buttonBounds.Left >= 0 && buttonBounds.Right <= toolbar.ActualWidth + 0.5,
                    $"A toolbar button is clipped at note width {width}.");
                Assert(button.Focusable && !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(button)),
                    "A toolbar button lacks keyboard access or an accessible name.");
            }
            if (width == NoteModel.MinWidth) Assert(toolbar.ActualHeight > 32, "The compact toolbar did not wrap.");
            if (width == NoteModel.DefaultWidth) Assert(toolbar.ActualHeight <= 32, "The default-width toolbar wastes a second row.");
        }
        var workArea = SystemParameters.WorkArea;
        window.Left = workArea.Right - 64;
        window.Top = workArea.Bottom - 64;
        Invoke(window, "CollapseToIcon");
        Assert(!viewModel.Topmost && window.Topmost, "Collapsing changed the saved pin preference.");
        var iconPosition = new Point(window.Left, window.Top);
        Equal(64, window.Width);
        Assert(!handles.IsHitTestVisible, "Collapsed icon still accepts window resize input.");
        Invoke(window, "RestoreFromIcon");
        Assert(!viewModel.Topmost && !window.Topmost, "Restoring did not recover the original pin preference.");
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
        VerifyEditorFeedback(window);
        VerifyLanguages();
        VerifySettingsLayout();
        VerifyNativeHotkeys();
        VerifyNativeStacking(window);
        window.CloseForApplication();
        VerifyShortcutNavigation(app);
    }

    private static void VerifyLanguages()
    {
        var english = new ResourceDictionary { Source = new Uri("/DeskStickyNotes;component/Localization/Strings.en-US.xaml", UriKind.Relative) };
        var keys = english.Keys.Cast<string>().Order().ToArray();
        var systemLanguage = LocalizationService.ResolveCulture("Auto");
        var sample = new NoteWindow(new NoteViewModel(new NoteModel(), 15));
        try
        {
            foreach (var option in LocalizationService.AvailableLanguages.Where(option => option.Code != "Auto"))
            {
                var dictionary = new ResourceDictionary { Source = new Uri($"/DeskStickyNotes;component/Localization/Strings.{option.Code}.xaml", UriKind.Relative) };
                Assert(keys.SequenceEqual(dictionary.Keys.Cast<string>().Order()), $"Missing or extra translations in {option.Code}.");
                foreach (var key in keys)
                {
                    var value = (string)dictionary[key];
                    Assert(!string.IsNullOrWhiteSpace(value) && !value.Contains("???", StringComparison.Ordinal), $"Empty or corrupted translation {option.Code}/{key}.");
                    var expected = Regex.Matches((string)english[key], @"\{\d+\}").Select(match => match.Value).Order();
                    var actual = Regex.Matches(value, @"\{\d+\}").Select(match => match.Value).Order();
                    Assert(expected.SequenceEqual(actual), $"Formatting placeholders differ in {option.Code}/{key}.");
                    _ = string.Format(value, "sample", 2, "1 KB");
                }
                LocalizationService.ApplyLanguage(option.Code);
                Equal(option.Code, LocalizationService.ActiveCultureName);
                Equal((string)dictionary["NoteTitle"], ((TextBlock)sample.FindName("NoteTitleText")).Text);
                var settings = new SettingsViewModel(new AppSettings(), _ => throw new Exception("Localization must not save."));
                var settingsWindow = new SettingsWindow(settings);
                try
                {
                    settings.SetHotkeyError(string.Format(LocalizationService.Get("HotkeyUnavailable"), "Ctrl+Alt+Shift+PageDown"));
                    var root = (FrameworkElement)settingsWindow.Content;
                    root.Measure(new Size(500, 420));
                    root.Arrange(new Rect(0, 0, 500, 420));
                    root.UpdateLayout();
                    var actions = Descendants<Button>(root).Where(button => button.Command == settings.SaveCommand || button.Command == settings.CancelCommand).ToList();
                    Equal(2, actions.Count);
                    foreach (var button in actions)
                    {
                        var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(new Point(), button.RenderSize));
                        Assert(bounds.Right <= 500 && bounds.Bottom <= 420 && bounds.Top >= 0, $"Actions clipped in {option.Code}.");
                    }
                    var recorder = (TextBox)settingsWindow.FindName("NewNoteHotkeyBox");
                    Assert(recorder.IsReadOnly && recorder.Text == HotkeyGesture.DefaultNewNote, "The shortcut recorder is not populated.");
                }
                finally { settingsWindow.Close(); }
            }
            // Auto must follow the original Windows UI language, not the last manual selection.
            LocalizationService.ApplyLanguage("Auto");
            Equal(systemLanguage, LocalizationService.ActiveCultureName);
        }
        finally { sample.CloseForApplication(); LocalizationService.ApplyLanguage("en-US"); }
    }

    private static void VerifyNativeHotkeys()
    {
        var created = 0;
        var switched = 0;
        using var service = new GlobalHotkeyService(() => created++, () => switched++);
        using var competitor = new GlobalHotkeyService(() => { }, () => { });
        Assert(service.TryApply("Ctrl+Alt+Shift+F7", "Ctrl+Alt+Shift+F8", out var error), $"Native registration failed: {error}");
        Assert(competitor.TryApply("Ctrl+Alt+Shift+F9", "", out _), "Cannot reserve the native conflict fixture.");
        var original = service.Registrations.ToArray();
        Assert(!service.TryApply("Ctrl+Alt+Shift+F6", "Ctrl+Alt+Shift+F9", out error), "Windows allowed two owners of the same shortcut.");
        Equal(HotkeyFailureReason.Unavailable, error!.Reason);
        Assert(original.SequenceEqual(service.Registrations), "Native conflict replaced the working bindings.");
        foreach (var registration in original) SendMessage(service.Handle, 0x0312, new IntPtr(registration.Id), IntPtr.Zero);
        Equal(1, created); Equal(1, switched);
        service.Suspend();
        foreach (var registration in original) SendMessage(service.Handle, 0x0312, new IntPtr(registration.Id), IntPtr.Zero);
        Equal(1, created); Equal(1, switched);
        Assert(service.Resume(out _), "Native shortcuts could not resume after capture.");
        Assert(service.TryApply("", "", out _), "Native shortcuts could not be disabled.");
        Assert(competitor.TryApply("Ctrl+Alt+Shift+F7", "Ctrl+Alt+Shift+F8", out _), "Native reservations leaked after disabling.");
    }

    private static void VerifyShortcutNavigation(RegressionApp app)
    {
        var methods = typeof(App);
        void InvokeApp(string method, params object[] arguments) => methods.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, arguments);
        var notes = (System.Collections.ObjectModel.ObservableCollection<NoteViewModel>)methods.GetField("_notes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
        var windows = (Dictionary<Guid, NoteWindow>)methods.GetField("_noteWindows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
        try
        {
            InvokeApp("CreateNoteFromHotkey");
            InvokeApp("CreateNoteFromHotkey");
            Equal(2, notes.Count);
            var first = notes[0];
            var second = notes[1];
            first.Visible = false;
            windows[first.Id].HideFromDesktop();
            InvokeApp("CycleNotes");
            Assert(first.Visible && windows[first.Id].IsVisible, "Cycling did not reveal a hidden note.");
            Invoke(windows[first.Id], "CollapseToIcon");
            InvokeApp("ShowNoteWindow", second, true);
            InvokeApp("CycleNotes");
            Equal(Visibility.Visible, ((FrameworkElement)windows[first.Id].FindName("ExpandedContent")).Visibility);
            Assert(!first.Topmost, "Quick switching changed the saved pin preference.");
            windows[first.Id].MinimizeToTaskbar();
            InvokeApp("CycleNotes");
            InvokeApp("CycleNotes");
            Equal(WindowState.Normal, windows[first.Id].WindowState);
            Equal(2, new NoteStorageService(app.DataDirectory).LoadNotes().Count);
        }
        finally
        {
            foreach (var window in windows.Values.ToArray()) window.CloseForApplication();
            if (Directory.Exists(app.DataDirectory)) Directory.Delete(app.DataDirectory, true);
        }
    }

    private static void SettingsDrafts()
    {
        var original = new AppSettings { DefaultColor = NotePalette.Yellow, DefaultFontSize = 15 };
        AppSettings? saved = null;
        var settings = new SettingsViewModel(original, value => saved = value);
        settings.DefaultColor = NotePalette.Green;
        settings.DefaultFontSize = 19;
        Equal(NotePalette.Get(NotePalette.Green).Background, settings.PreviewBackground);
        Equal(NotePalette.Get(NotePalette.Green).Header, settings.PreviewHeader);
        settings.CancelCommand.Execute(null);
        Assert(saved is null, "Cancel saved a settings draft.");
        Equal(NotePalette.Yellow, original.DefaultColor);
        Equal(15d, original.DefaultFontSize);
        settings.SaveCommand.Execute(null);
        Assert(saved is not null, "Save did not commit the draft.");
        Equal(NotePalette.Green, saved!.DefaultColor);
        Equal(19d, saved.DefaultFontSize);
    }

    private static void VerifySettingsLayout()
    {
        var viewModel = new SettingsViewModel(new AppSettings(), _ => throw new Exception("Layout must not save settings."));
        var window = new SettingsWindow(viewModel);
        try
        {
            var content = (FrameworkElement)window.Content;
            foreach (var size in new[] { new Size(600, 760), new Size(500, 420) })
            {
                content.Measure(size);
                content.Arrange(new Rect(new Point(), size));
                content.UpdateLayout();
                foreach (var button in Descendants<Button>(content).Where(button => button.Command == viewModel.SaveCommand || button.Command == viewModel.CancelCommand))
                {
                    var bounds = button.TransformToAncestor(content).TransformBounds(new Rect(new Point(), button.RenderSize));
                    Assert(bounds.Top >= 0 && bounds.Bottom <= size.Height && bounds.Right <= size.Width,
                        "Settings actions are clipped on a compact display.");
                }
                var scroller = (ScrollViewer)window.FindName("SettingsScroller");
                Assert(scroller.ActualHeight > 0 && scroller.ViewportHeight < scroller.ExtentHeight,
                    "Settings content cannot scroll when vertical space is constrained.");
            }
            var colors = (ListBox)window.FindName("ColorPicker");
            var language = (System.Windows.Controls.ComboBox)window.FindName("LanguagePicker");
            var selectedLanguage = ((LanguageOption)language.SelectedItem).DisplayName;
            Assert(Descendants<TextBlock>(language).Any(text => text.Text == selectedLanguage),
                "The language picker displays an object name instead of its localized label.");
            Equal(NotePalette.Names.Count, colors.Items.Count);
            colors.SelectedValue = NotePalette.Pink;
            Equal(NotePalette.Pink, viewModel.DefaultColor);
            var toggle = (System.Windows.Controls.CheckBox)window.FindName("DesktopToggle");
            toggle.IsChecked = false;
            Assert(!viewModel.KeepNotesVisibleAfterShowDesktop, "The desktop switch did not update the draft.");
            Assert(!string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(toggle)),
                "A settings switch lacks an accessible name.");
            using var icon = TrayService.LoadTrayIcon();
            Assert(icon is not null, "The new notification-area icon could not be loaded.");
        }
        finally { window.Close(); }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T result) yield return result;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void VerifyEditorFeedback(NoteWindow window)
    {
        var editor = (RichTextBox)window.FindName("Editor");
        var hint = (TextBlock)window.FindName("EmptyNoteHint");
        Equal(Visibility.Visible, hint.Visibility);
        var run = new Run("Regression sample") { FontWeight = FontWeights.Bold };
        editor.Document.Blocks.Clear();
        editor.Document.Blocks.Add(new Paragraph(run));
        editor.Selection.Select(run.ContentStart, run.ContentEnd);
        Invoke(window, "UpdateEditorFeedback");
        Equal(Visibility.Collapsed, hint.Visibility);
        var bold = Descendants<Button>((WrapPanel)window.FindName("EditorToolbar")).Single(button => Equals(button.Tag, "Bold"));
        Assert(Selector.GetIsSelected(bold), "Bold text does not show an active formatting state.");
        editor.Selection.ClearAllProperties();
        Invoke(window, "UpdateEditorFeedback");
        Assert(!Selector.GetIsSelected(bold), "The bold indicator did not clear.");
        editor.Document.Blocks.Clear();
        editor.Document.Blocks.Add(new Paragraph(new InlineUIContainer(new System.Windows.Controls.Image())));
        Invoke(window, "UpdateEditorFeedback");
        Equal(Visibility.Collapsed, hint.Visibility);
        editor.Document.Blocks.Clear();
        Invoke(window, "UpdateEditorFeedback");
        Equal(Visibility.Visible, hint.Visibility);
    }

    private static void VerifyNativeStacking(NoteWindow first)
    {
        var secondModel = new NoteViewModel(new NoteModel { Title = "Stacking regression", Topmost = true }, 14);
        var second = new NoteWindow(secondModel);
        using var service = new DesktopNoteRecoveryService(() => true);
        try
        {
            first.Left = second.Left = 180;
            first.Top = second.Top = 120;
            ((NoteViewModel)first.DataContext).Topmost = true;
            first.Show();
            second.Show();
            service.Register(first);
            service.Register(second);
            service.NoteActivated(first);
            PumpDispatcher(TimeSpan.FromMilliseconds(300));
            var foreground = GetForegroundWindow();
            service.CheckWindows(true);
            var firstHandle = new WindowInteropHelper(first).Handle;
            var secondHandle = new WindowInteropHelper(second).Handle;
            Assert(IsAbove(firstHandle, secondHandle), "The last-used topmost note did not stay above its peer.");
            var zOrderWrites = 0;
            IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                if (message == 0x0046 && lParam != IntPtr.Zero)
                {
                    var position = Marshal.PtrToStructure<NativeWindowPos>(lParam);
                    // Count recovery promotions, excluding unrelated WPF frame/layout updates.
                    if ((position.Flags & 0x0253) == 0x0253 && (position.Flags & 0x0004) == 0) zOrderWrites++;
                }
                return IntPtr.Zero;
            }
            var firstSource = HwndSource.FromHwnd(firstHandle)!;
            var secondSource = HwndSource.FromHwnd(secondHandle)!;
            firstSource.AddHook(Hook);
            secondSource.AddHook(Hook);
            try
            {
                for (var tick = 0; tick < 240; tick++) service.CheckWindows(true);
                PumpDispatcher(TimeSpan.FromMilliseconds(1250));
                Equal(0, zOrderWrites);
                Equal(foreground, GetForegroundWindow());
                Assert(IsAbove(firstHandle, secondHandle), "Idle recovery reversed overlapping notes.");
                ShowWindow(firstHandle, 6 /* SW_MINIMIZE: mimic shell suppression without a Win key shortcut */);
                Assert(IsIconic(firstHandle), "The native suppression test did not minimize its sample window.");
                service.RequestRecovery();
                service.CheckWindows(true);
                PumpDispatcher(TimeSpan.FromMilliseconds(300));
                Assert(!IsIconic(firstHandle), "A shell-minimized sample note was not restored.");
                Equal(foreground, GetForegroundWindow());
                Assert(IsAbove(firstHandle, secondHandle), "Restoring a suppressed note lost the group order.");
                // An unpinned note stays in the normal band after recovery.
                ((NoteViewModel)first.DataContext).Topmost = false;
                service.CheckWindows(false);
                service.CheckWindows(true);
                Assert(IsAbove(secondHandle, firstHandle), "Recovery raised a normal note above a pinned note.");
                first.MinimizeToTaskbar();
                Assert(!((IDesktopRecoverableNote)first).CanRecoverAfterShowDesktop,
                    "An intentional taskbar minimize remains eligible for shell recovery.");
                PumpDispatcher(TimeSpan.FromMilliseconds(300));
                Assert(IsIconic(firstHandle), "The coordinator undid an intentional taskbar minimize.");
                ((NoteViewModel)first.DataContext).Visible = false;
                first.HideFromDesktop();
                service.RequestRecovery();
                service.CheckWindows(false);
                service.CheckWindows(true);
                Assert(!first.IsVisible, "A deliberately hidden note was restored by desktop recovery.");
            }
            finally { firstSource.RemoveHook(Hook); secondSource.RemoveHook(Hook); }
        }
        finally { second.CloseForApplication(); }
    }

    private static bool IsAbove(IntPtr upper, IntPtr lower)
    {
        for (var next = GetWindow(lower, 3 /* GW_HWNDPREV */); next != IntPtr.Zero; next = GetWindow(next, 3))
            if (next == upper) return true;
        return false;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeWindowPos
    {
        public IntPtr Window, InsertAfter;
        public int X, Y, Width, Height;
        public uint Flags;
    }

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static int RenderPreview(string outputDirectory)
    {
        // Compiled WPF views with sample content only. No startup, tray, or user storage.
        var app = new RegressionApp();
        LocalizationService.ApplyLanguage("zh-Hans");
        Directory.CreateDirectory(outputDirectory);
        foreach (var (width, color, name, text) in new[]
        {
            (540, NotePalette.Yellow, "工作计划", "本周要做的事\n\n☐ 整理产品想法\n☐ 完成界面优化\n☑ 留一点时间思考\n\n把灵感留在手边，专注眼前的一件事。"),
            (300, NotePalette.Green, "灵感与提醒 · 一个很长的便签标题", "一张小便签，也能从容记录。\n\n支持待办、文本切片和文件拖入。"),
            (420, NotePalette.Blue, "随手记", "")
        })
        {
            var model = new NoteModel { Title = name, Width = width, Height = 400, Color = color, TextContent = text, Topmost = color == NotePalette.Yellow };
            var window = new NoteWindow(new NoteViewModel(model, 15));
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(width, 400));
            root.Arrange(new Rect(0, 0, width, 400));
            root.UpdateLayout();
            Invoke(window, "UpdateEditorFeedback");
            var bitmap = new RenderTargetBitmap(width, 400, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(outputDirectory, $"note-{width}.png"))) encoder.Save(file);
            window.CloseForApplication();
        }
        foreach (var language in LocalizationService.AvailableLanguages.Where(option => option.Code != "Auto").Select(option => option.Code))
        {
            const int width = 600, height = 760;
            LocalizationService.ApplyLanguage(language);
            var window = new SettingsWindow(new SettingsViewModel(new AppSettings(), _ => { }));
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(outputDirectory, $"settings-{language}.png"))) encoder.Save(file);
            var scroller = (ScrollViewer)window.FindName("SettingsScroller");
            var recorder = (TextBox)window.FindName("NewNoteHotkeyBox");
            var bounds = recorder.TransformToAncestor((FrameworkElement)scroller.Content).TransformBounds(new Rect(new Point(), recorder.RenderSize));
            scroller.ScrollToVerticalOffset(Math.Max(0, bounds.Top - 120));
            root.UpdateLayout();
            var hotkeyBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            hotkeyBitmap.Render(root);
            var hotkeyEncoder = new PngBitmapEncoder();
            hotkeyEncoder.Frames.Add(BitmapFrame.Create(hotkeyBitmap));
            using (var file = File.Create(Path.Combine(outputDirectory, $"settings-hotkeys-{language}.png"))) hotkeyEncoder.Save(file);
            window.Close();
        }
        LocalizationService.ApplyLanguage("zh-Hans");
        var sampleNotes = new[] { new TrayNote(Guid.NewGuid(), "工作计划", true), new TrayNote(Guid.NewGuid(), "灵感与提醒", false) };
        using var tray = new TrayService(() => { }, () => { }, () => { }, () => { }, () => { }, () => sampleNotes, (_, _) => { });
        using var menu = tray.CreateContextMenu();
        menu.CreateControl();
        menu.PerformLayout();
        menu.Size = menu.GetPreferredSize(System.Drawing.Size.Empty);
        using (var bitmap = new System.Drawing.Bitmap(menu.Width, menu.Height))
        {
            menu.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, menu.Size));
            bitmap.Save(Path.Combine(outputDirectory, "tray-menu.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        RenderOverview(app, outputDirectory);
        return 0;
    }

    private static void RenderOverview(App app, string outputDirectory)
    {
        var visual = new DrawingVisual();
        var ink = new SolidColorBrush(System.Windows.Media.Color.FromRgb(41, 54, 47));
        void Label(DrawingContext context, string value, double x, double y, double size = 16, System.Windows.Media.Brush? brush = null)
        {
            var text = new FormattedText(value, CultureInfo.GetCultureInfo("zh-CN"), System.Windows.FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"), size, brush ?? ink, 1);
            context.DrawText(text, new Point(x, y));
        }
        void Screenshot(DrawingContext context, string file, Rect bounds)
        {
            var bitmap = new BitmapImage(new Uri(Path.GetFullPath(Path.Combine(outputDirectory, file))));
            context.DrawImage(bitmap, bounds);
        }
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 243, 235)), null, new Rect(0, 0, 1230, 760));
            Label(context, "桌面便签 · 界面预览", 28, 20, 24);
            Label(context, "便签", 28, 74);
            Label(context, "设置", 448, 74);
            Label(context, "托盘菜单", 956, 74);
            Screenshot(context, "note-540.png", new Rect(20, 104, 408, 302));
            Screenshot(context, "settings-zh-Hans.png", new Rect(444, 104, 480, 608));
            Screenshot(context, "tray-menu.png", new Rect(952, 104, 244, 330));
            Label(context, "托盘图标 · 浅色与深色背景", 28, 460);
            context.DrawRoundedRectangle(System.Windows.Media.Brushes.White, null, new Rect(28, 500, 386, 62), 8, 8);
            context.DrawRoundedRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 43, 42)), null, new Rect(28, 572, 386, 62), 8, 8);
            var icon = (ImageSource)app.FindResource("TrayMark");
            var x = 48d;
            foreach (var size in new[] { 16, 20, 24, 32 })
            {
                context.DrawImage(icon, new Rect(x, 531 - size / 2d, size, size));
                context.DrawImage(icon, new Rect(x, 603 - size / 2d, size, size));
                Label(context, size.ToString(CultureInfo.InvariantCulture), x + size + 8, 524, 12);
                Label(context, size.ToString(CultureInfo.InvariantCulture), x + size + 8, 596, 12, System.Windows.Media.Brushes.White);
                x += 88;
            }
            Label(context, "使用示例内容的实际控件渲染", 28, 700, 12);
        }
        var output = new RenderTargetBitmap(1230, 760, 96, 96, PixelFormats.Pbgra32);
        output.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(output));
        using var stream = File.Create(Path.Combine(outputDirectory, "interface-overview.png"));
        encoder.Save(stream);
    }

    private sealed class RegressionApp : App
    {
        public string DataDirectory { get; }
        public RegressionApp() : this(Path.Combine(Path.GetTempPath(), "DeskStickyNotes.Tests", Guid.NewGuid().ToString("N"))) { }
        private RegressionApp(string directory) : base(new NoteStorageService(directory))
        {
            DataDirectory = directory;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            foreach (var resource in new[] { "Localization/Strings.en-US.xaml", "Themes/Controls.xaml" })
                Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri($"/DeskStickyNotes;component/{resource}", UriKind.Relative)
                });
        }
        // Pumping the WPF dispatcher must never start the real app, acquire its
        // singleton mutex, change startup registration, or load/save user notes.
        protected override void OnStartup(StartupEventArgs e) { }
        protected override void OnExit(ExitEventArgs e) { }
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
        first.PerformClick();
        second.PerformClick();
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
