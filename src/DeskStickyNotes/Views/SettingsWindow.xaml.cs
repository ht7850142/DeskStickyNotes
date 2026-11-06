using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using DeskStickyNotes.Models;
using DeskStickyNotes.Services;
using DeskStickyNotes.ViewModels;

namespace DeskStickyNotes.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly Action<bool>? _setHotkeyCapture;
    private bool _capturing;

    public SettingsWindow(SettingsViewModel viewModel, Action<bool>? setHotkeyCapture = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _setHotkeyCapture = setHotkeyCapture;
        DataContext = _viewModel;
        // Keep the action footer reachable on small/high-DPI displays.
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 48);
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 48);
        MinHeight = Math.Min(MinHeight, Height);
        MinWidth = Math.Min(MinWidth, Width);
        _viewModel.CloseRequested += OnCloseRequested;
        Deactivated += (_, _) => SetCapture(false);
        Activated += (_, _) => SetCapture(NewNoteHotkeyBox.IsKeyboardFocusWithin || NextNoteHotkeyBox.IsKeyboardFocusWithin);
    }

    protected override void OnClosed(EventArgs e)
    {
        SetCapture(false);
        _viewModel.CloseRequested -= OnCloseRequested;
        base.OnClosed(e);
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void SetCapture(bool capturing)
    {
        if (_capturing == capturing) return;
        _capturing = capturing;
        _setHotkeyCapture?.Invoke(capturing);
    }

    private void Hotkey_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => SetCapture(true);
    private void Hotkey_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => SetCapture(false);

    private void Hotkey_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift) return;
        e.Handled = true;
        if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            ((UIElement)sender).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            return;
        }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        string value;
        if (key is Key.Back or Key.Delete && Keyboard.Modifiers == ModifierKeys.None) value = "";
        else if (HotkeyGesture.TryCreate(Keyboard.Modifiers, key, out var gesture)) value = gesture.ToString();
        else { _viewModel.SetHotkeyError(LocalizationService.Get("HotkeyInvalid")); return; }
        if (ReferenceEquals(sender, NewNoteHotkeyBox)) _viewModel.NewNoteHotkey = value;
        else _viewModel.NextNoteHotkey = value;
    }
}
