using System.Windows.Input;

namespace DeskStickyNotes.Models;

public readonly record struct HotkeyGesture(ModifierKeys Modifiers, Key Key)
{
    public const string DefaultNewNote = "Ctrl+Alt+N";
    public const string DefaultNextNote = "Ctrl+Alt+PageDown";

    public static bool TryCreate(ModifierKeys modifiers, Key key, out HotkeyGesture gesture)
    {
        gesture = default;
        const ModifierKeys allowed = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows;
        if ((modifiers & ~allowed) != 0 || (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0
            || key is Key.None or Key.System or Key.ImeProcessed or Key.DeadCharProcessed
                or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            || !Enum.IsDefined(key)) return false;
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey is <= 0 or > 255) return false;
        gesture = new HotkeyGesture(modifiers, key);
        return true;
    }

    public static bool TryParse(string? value, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('+', StringSplitOptions.TrimEntries);
        var modifiers = ModifierKeys.None;
        foreach (var part in parts[..^1])
        {
            var modifier = part.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => ModifierKeys.Control,
                "ALT" => ModifierKeys.Alt,
                "SHIFT" => ModifierKeys.Shift,
                "WIN" or "WINDOWS" => ModifierKeys.Windows,
                _ => ModifierKeys.None
            };
            if (modifier == ModifierKeys.None || (modifiers & modifier) != 0) return false;
            modifiers |= modifier;
        }
        var keyName = parts[^1].Equals("Esc", StringComparison.OrdinalIgnoreCase) ? "Escape" : parts[^1];
        return Enum.TryParse<Key>(keyName, true, out var key) && TryCreate(modifiers, key, out gesture);
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(Key switch { Key.PageDown => "PageDown", Key.PageUp => "PageUp", Key.Enter => "Enter", Key.Escape => "Esc", _ => Key.ToString() });
        return string.Join('+', parts);
    }
}
