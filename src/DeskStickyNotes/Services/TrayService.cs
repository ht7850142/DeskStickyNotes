using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DeskStickyNotes.Services;

public sealed record TrayNote(Guid Id, string Title, bool Visible);

public sealed class TrayService : IDisposable
{
    private readonly Action _newNote;
    private readonly Action _showAllNotes;
    private readonly Action _hideAllNotes;
    private readonly Action _showSettings;
    private readonly Action _exit;
    private readonly Func<IReadOnlyList<TrayNote>> _getNotes;
    private readonly Action<Guid, bool> _setNoteVisibility;
    private Forms.NotifyIcon? _notifyIcon;
    private Drawing.Icon? _ownedIcon;
    private Forms.ToolStripMenuItem? _newNoteItem;
    private Forms.ToolStripMenuItem? _showAllItem;
    private Forms.ToolStripMenuItem? _hideAllItem;
    private Forms.ToolStripMenuItem? _settingsItem;
    private Forms.ToolStripMenuItem? _exitItem;
    private Forms.ToolStripMenuItem? _noteListItem;

    public TrayService(
        Action newNote,
        Action showAllNotes,
        Action hideAllNotes,
        Action showSettings,
        Action exit,
        Func<IReadOnlyList<TrayNote>> getNotes,
        Action<Guid, bool> setNoteVisibility)
    {
        _newNote = newNote;
        _showAllNotes = showAllNotes;
        _hideAllNotes = hideAllNotes;
        _showSettings = showSettings;
        _exit = exit;
        _getNotes = getNotes;
        _setNoteVisibility = setNoteVisibility;
    }

    public void Show()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        var menu = CreateContextMenu();
        _ownedIcon = LoadAppIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = _ownedIcon ?? Drawing.SystemIcons.Application,
            Visible = true
        };
        _notifyIcon.MouseDoubleClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left) _showAllNotes();
        };
        RefreshText();
    }

    internal Forms.ContextMenuStrip CreateContextMenu()
    {
        var menu = new Forms.ContextMenuStrip { ShowItemToolTips = true };
        _newNoteItem = new Forms.ToolStripMenuItem("", null, (_, _) => _newNote());
        _noteListItem = new Forms.ToolStripMenuItem();
        _showAllItem = new Forms.ToolStripMenuItem("", null, (_, _) => _showAllNotes());
        _hideAllItem = new Forms.ToolStripMenuItem("", null, (_, _) => _hideAllNotes());
        _settingsItem = new Forms.ToolStripMenuItem("", null, (_, _) => _showSettings());
        _exitItem = new Forms.ToolStripMenuItem("", null, (_, _) => _exit());

        menu.Items.Add(_newNoteItem);
        menu.Items.Add(_noteListItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_showAllItem);
        menu.Items.Add(_hideAllItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_settingsItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_exitItem);

        menu.Opening += (_, _) => RefreshNoteList();
        RefreshText();
        return menu;
    }

    internal void RefreshNoteList()
    {
        if (_noteListItem is null) return;
        while (_noteListItem.DropDownItems.Count > 0)
        {
            var item = _noteListItem.DropDownItems[0];
            _noteListItem.DropDownItems.RemoveAt(0);
            item.Dispose();
        }

        var notes = _getNotes();
        _noteListItem.Text = $"{LocalizationService.Get("TrayNoteList")} ({notes.Count})";
        if (notes.Count == 0)
        {
            _noteListItem.DropDownItems.Add(new Forms.ToolStripMenuItem(LocalizationService.Get("TrayNoNotes")) { Enabled = false });
            return;
        }

        for (var index = 0; index < notes.Count; index++)
        {
            var note = notes[index];
            var title = string.IsNullOrWhiteSpace(note.Title) ? $"{LocalizationService.Get("NoteTitle")} {index + 1}" : note.Title;
            var noteItem = new Forms.ToolStripMenuItem(title.Replace("&", "&&"))
            {
                Checked = note.Visible,
                Tag = note.Id,
                ToolTipText = $"{title}\n{LocalizationService.Get(note.Visible ? "TrayNoteVisible" : "TrayNoteHidden")}"
            };
            noteItem.DropDownItems.Add(new Forms.ToolStripMenuItem(LocalizationService.Get("TrayShowNote"), null,
                (_, _) => _setNoteVisibility(note.Id, true)) { Checked = note.Visible });
            noteItem.DropDownItems.Add(new Forms.ToolStripMenuItem(LocalizationService.Get("TrayHideNote"), null,
                (_, _) => _setNoteVisibility(note.Id, false)) { Checked = !note.Visible });
            _noteListItem.DropDownItems.Add(noteItem);
        }
    }

    public void RefreshText()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Text = LocalizationService.Get("AppName");
        }

        if (_newNoteItem is not null)
        {
            _newNoteItem.Text = LocalizationService.Get("TrayNewNote");
        }

        if (_showAllItem is not null)
        {
            _showAllItem.Text = LocalizationService.Get("TrayShowAll");
        }

        if (_hideAllItem is not null)
        {
            _hideAllItem.Text = LocalizationService.Get("TrayHideAll");
        }

        if (_settingsItem is not null)
        {
            _settingsItem.Text = LocalizationService.Get("TraySettings");
        }

        if (_exitItem is not null)
        {
            _exitItem.Text = LocalizationService.Get("TrayExit");
        }
        RefreshNoteList();
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _notifyIcon = null;
        _ownedIcon?.Dispose();
        _ownedIcon = null;
    }

    private static Drawing.Icon? LoadAppIcon()
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/AppIcon.ico"));
            if (info?.Stream is null)
            {
                return null;
            }

            using var icon = new Drawing.Icon(info.Stream);
            return (Drawing.Icon)icon.Clone();
        }
        catch
        {
            return null;
        }
    }
}
