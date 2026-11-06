using DeskStickyNotes.Models;

namespace DeskStickyNotes.Services;

internal enum HotkeyAction { NewNote, NextNote }
internal enum HotkeyFailureReason { Invalid, Duplicate, Unavailable }
internal sealed record HotkeyFailure(HotkeyFailureReason Reason, string Shortcut)
{
    public string ToUserMessage() => Reason switch
    {
        HotkeyFailureReason.Invalid => LocalizationService.Get("HotkeyInvalid"),
        HotkeyFailureReason.Duplicate => LocalizationService.Get("HotkeyDuplicate"),
        _ => string.Format(LocalizationService.Get("HotkeyUnavailable"), Shortcut)
    };
}

internal interface IHotkeyRegistrar
{
    bool Register(int id, HotkeyGesture gesture);
    void Unregister(int id);
}

internal sealed record HotkeyRegistration(int Id, HotkeyAction Action, HotkeyGesture Gesture);

/// <summary>Reserves replacements before releasing working shortcuts, including swaps.</summary>
internal sealed class HotkeyRegistrationManager(IHotkeyRegistrar registrar) : IDisposable
{
    private readonly List<HotkeyRegistration> _entries = [];
    private readonly HashSet<int> _active = [];
    private int _nextId = 1000;
    private int _suspensions;
    public IReadOnlyList<HotkeyRegistration> Entries => _entries.AsReadOnly();
    public event Action<HotkeyAction>? Invoked;

    public bool TryInitialize(string? newNote, string? nextNote, out HotkeyFailure? failure)
    {
        if (TryApply(newNote, nextNote, out failure)) return true;
        var firstFailure = failure;
        // At startup there is no older working pair. Keep whichever action can
        // be registered instead of disabling both when only one is occupied.
        var hasNew = TryApply(newNote, "", out _);
        TryApply(hasNew ? newNote : "", nextNote, out _);
        failure = firstFailure;
        return false;
    }

    public bool TryApply(string? newNote, string? nextNote, out HotkeyFailure? failure)
    {
        failure = null;
        var desired = new List<(HotkeyAction Action, HotkeyGesture Gesture)>();
        foreach (var (action, text) in new[] { (HotkeyAction.NewNote, newNote), (HotkeyAction.NextNote, nextNote) })
        {
            if (string.IsNullOrWhiteSpace(text)) continue; // An empty field disables that action.
            if (!HotkeyGesture.TryParse(text, out var gesture))
            {
                failure = new(HotkeyFailureReason.Invalid, text);
                return false;
            }
            if (desired.Any(item => item.Gesture == gesture))
            {
                failure = new(HotkeyFailureReason.Duplicate, text);
                return false;
            }
            desired.Add((action, gesture));
        }

        var replacements = new List<HotkeyRegistration>();
        var reserved = new List<int>();
        foreach (var item in desired)
        {
            var existing = _entries.Find(entry => entry.Gesture == item.Gesture);
            var registration = existing is not null ? existing with { Action = item.Action }
                : new HotkeyRegistration(NextId(), item.Action, item.Gesture);
            if (!_active.Contains(registration.Id))
            {
                if (!registrar.Register(registration.Id, registration.Gesture))
                {
                    foreach (var id in reserved) registrar.Unregister(id);
                    failure = new(HotkeyFailureReason.Unavailable, registration.Gesture.ToString());
                    return false;
                }
                reserved.Add(registration.Id);
            }
            replacements.Add(registration);
        }
        foreach (var old in _entries.Where(old => replacements.All(entry => entry.Id != old.Id)))
            if (_active.Remove(old.Id)) registrar.Unregister(old.Id);
        _entries.Clear();
        _entries.AddRange(replacements);
        _active.UnionWith(reserved);
        if (_suspensions > 0) ReleaseActive();
        return true;
    }

    public void Suspend()
    {
        if (_suspensions++ == 0) ReleaseActive();
    }

    public bool Resume(out HotkeyFailure? failure)
    {
        failure = null;
        if (_suspensions == 0 || --_suspensions > 0) return true;
        return TryApply(_entries.Find(entry => entry.Action == HotkeyAction.NewNote)?.Gesture.ToString(),
            _entries.Find(entry => entry.Action == HotkeyAction.NextNote)?.Gesture.ToString(), out failure);
    }

    public void Dispatch(int id)
    {
        if (_suspensions != 0 || !_active.Contains(id)) return;
        if (_entries.Find(entry => entry.Id == id) is { } entry) Invoked?.Invoke(entry.Action);
    }

    private int NextId()
    {
        while (true)
        {
            if (_nextId > 0xBFFF) _nextId = 1000;
            var id = _nextId++;
            if (_entries.All(entry => entry.Id != id)) return id;
        }
    }

    private void ReleaseActive()
    {
        foreach (var id in _active) registrar.Unregister(id);
        _active.Clear();
    }

    public void Dispose() { ReleaseActive(); _entries.Clear(); }
}
