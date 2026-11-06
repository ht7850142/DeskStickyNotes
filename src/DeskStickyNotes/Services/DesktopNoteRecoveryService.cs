using System.Windows.Threading;

namespace DeskStickyNotes.Services;

internal interface IDesktopRecoverableNote
{
    bool CanRecoverAfterShowDesktop { get; }
    bool IsSuppressedByShell { get; }
    void RecoverAfterShowDesktop(bool restoreVisibility);
}

/// <summary>
/// Restores notes as one ordered group. Merely staying on the desktop must never
/// produce a stream of HWND_TOP/HWND_TOPMOST requests from competing windows.
/// All calls, including registration and activation, run on the UI dispatcher.
/// </summary>
internal sealed class DesktopNoteRecoveryService : IDisposable
{
    private const int MaximumRecoveryAttempts = 3;
    private readonly List<Entry> _notes = [];
    private readonly DispatcherTimer _timer;
    private bool _wasDesktopForeground;
    private bool _recoveryRequested;
    private bool _isRecovering;

    public DesktopNoteRecoveryService(Func<bool> isDesktopForeground)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += (_, _) => CheckWindows(isDesktopForeground());
    }

    public void Register(IDesktopRecoverableNote note)
    {
        if (_notes.Any(entry => ReferenceEquals(entry.Note, note))) return;
        _notes.Add(new Entry(note));
        _timer.Start();
    }

    public void Unregister(IDesktopRecoverableNote note)
    {
        _notes.RemoveAll(entry => ReferenceEquals(entry.Note, note));
        if (_notes.Count == 0)
        {
            _timer.Stop();
            _wasDesktopForeground = false;
            _recoveryRequested = false;
        }
    }

    public void NoteActivated(IDesktopRecoverableNote note)
    {
        // Oldest to newest: recovery raises the last-used note last, separately
        // respecting Windows' normal and topmost bands.
        var entry = _notes.Find(entry => ReferenceEquals(entry.Note, note));
        if (entry is null || _isRecovering) return;
        _notes.Remove(entry);
        _notes.Add(entry);
    }

    public void RequestRecovery()
    {
        // Shell notifications from a whole group coalesce into the next tick.
        // Ignore notifications caused by our own ShowWindow/SetWindowPos calls.
        if (!_isRecovering) _recoveryRequested = true;
    }

    internal void CheckWindows(bool desktopForeground)
    {
        if (_isRecovering) return;
        var enteredDesktop = desktopForeground && !_wasDesktopForeground;
        _wasDesktopForeground = desktopForeground;
        var recoveryRequested = _recoveryRequested;
        _recoveryRequested = false;

        var candidates = _notes.Where(entry => entry.Note.CanRecoverAfterShowDesktop)
            .Select(entry => (Entry: entry, Suppressed: entry.Note.IsSuppressedByShell)).ToList();
        foreach (var candidate in candidates)
        {
            if (enteredDesktop || !candidate.Suppressed) candidate.Entry.Attempts = 0;
        }

        var suppressed = candidates.Where(candidate => candidate.Suppressed
            && candidate.Entry.Attempts < MaximumRecoveryAttempts).ToList();
        // A cloaked window on another virtual desktop is not itself a reason to
        // recover. Only desktop entry or a genuine hide/minimize notification is.
        if (!enteredDesktop && (!(desktopForeground || recoveryRequested) || suppressed.Count == 0)) return;

        _isRecovering = true;
        try
        {
            foreach (var candidate in candidates)
            {
                if (candidate.Suppressed && candidate.Entry.Attempts >= MaximumRecoveryAttempts) continue;
                if (candidate.Suppressed) candidate.Entry.Attempts++;
                candidate.Entry.Note.RecoverAfterShowDesktop(candidate.Suppressed);
            }
        }
        finally { _isRecovering = false; }
    }

    public void Dispose()
    {
        _timer.Stop();
        _notes.Clear();
    }

    private sealed class Entry(IDesktopRecoverableNote note)
    {
        public IDesktopRecoverableNote Note { get; } = note;
        public int Attempts { get; set; }
    }
}
