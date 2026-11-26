using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using DeskStickyNotes.Models;

namespace DeskStickyNotes.Services;

internal sealed class GlobalHotkeyService : IDisposable, IHotkeyRegistrar
{
    private readonly HwndSource _source;
    private readonly HotkeyRegistrationManager _manager;
    internal IntPtr Handle => _source.Handle;
    internal IReadOnlyList<HotkeyRegistration> Registrations => _manager.Entries;

    public GlobalHotkeyService(Action<HotkeyAction> invoke)
    {
        _source = new HwndSource(new HwndSourceParameters("DeskStickyNotes.GlobalHotkeys")
        {
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE: no visible or taskbar window.
            WindowStyle = 0
        });
        _manager = new HotkeyRegistrationManager(this);
        _manager.Invoked += invoke;
        _source.AddHook(WndProc);
    }

    public bool TryApply(AppSettings settings, out HotkeyFailure? failure) => _manager.TryApply(settings, out failure);
    public bool TryInitialize(AppSettings settings, out HotkeyFailure? failure) => _manager.TryInitialize(settings, out failure);
    public void Suspend() => _manager.Suspend();
    public bool Resume(out HotkeyFailure? failure) => _manager.Resume(out failure);

    bool IHotkeyRegistrar.Register(int id, HotkeyGesture gesture) =>
        RegisterHotKey(_source.Handle, id, (uint)gesture.Modifiers | 0x4000 /* MOD_NOREPEAT */, (uint)KeyInterop.VirtualKeyFromKey(gesture.Key));
    void IHotkeyRegistrar.Unregister(int id) => UnregisterHotKey(_source.Handle, id);

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312 /* WM_HOTKEY */) { handled = true; _manager.Dispatch(wParam.ToInt32()); }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _manager.Dispose();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
