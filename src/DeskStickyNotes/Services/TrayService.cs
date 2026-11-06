using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using System.Globalization;

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
    private Forms.ContextMenuStrip? _menu;
    private Forms.ToolStripLabel? _header;
    private readonly List<Drawing.Image> _menuImages = [];
    private Drawing.Font? _menuFont;
    private Drawing.Font? _emphasisFont;
    private string? _fontFamily;

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
        _ownedIcon = LoadTrayIcon();
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
        if (_menu is not null) return _menu;
        _fontFamily = LocalizationService.UiFontName;
        _menuFont = new Drawing.Font(_fontFamily, 9, Drawing.FontStyle.Regular);
        _emphasisFont = new Drawing.Font(_menuFont, Drawing.FontStyle.Bold);
        var menu = _menu = new Forms.ContextMenuStrip
        {
            ShowItemToolTips = true,
            Font = _menuFont,
            Renderer = TrayMenuVisuals.CreateRenderer(),
            Padding = new Forms.Padding(5, 7, 5, 7),
            ImageScalingSize = Forms.SystemInformation.SmallIconSize
        };
        _header = new Forms.ToolStripLabel
        {
            TextAlign = Drawing.ContentAlignment.MiddleLeft,
            Font = _emphasisFont,
            Padding = new Forms.Padding(9, 4, 14, 10)
        };
        _newNoteItem = new Forms.ToolStripMenuItem("", null, (_, _) => _newNote());
        _noteListItem = new Forms.ToolStripMenuItem();
        _showAllItem = new Forms.ToolStripMenuItem("", null, (_, _) => _showAllNotes());
        _hideAllItem = new Forms.ToolStripMenuItem("", null, (_, _) => _hideAllNotes());
        _settingsItem = new Forms.ToolStripMenuItem("", null, (_, _) => _showSettings());
        _exitItem = new Forms.ToolStripMenuItem("", null, (_, _) => _exit());

        var actionItems = new[] { _newNoteItem, _noteListItem, _showAllItem, _hideAllItem, _settingsItem, _exitItem };
        var glyphs = new[] { TrayMenuVisuals.Glyph.New, TrayMenuVisuals.Glyph.Notes, TrayMenuVisuals.Glyph.Show,
            TrayMenuVisuals.Glyph.Hide, TrayMenuVisuals.Glyph.Settings, TrayMenuVisuals.Glyph.Exit };
        for (var index = 0; index < actionItems.Length; index++)
        {
            var image = TrayMenuVisuals.CreateGlyph(glyphs[index]);
            _menuImages.Add(image);
            actionItems[index].Image = image;
            actionItems[index].Padding = new Forms.Padding(4, 6, 14, 6);
        }
        _newNoteItem.Font = _emphasisFont;

        menu.Items.Add(_header);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_newNoteItem);
        menu.Items.Add(_noteListItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_showAllItem);
        menu.Items.Add(_hideAllItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_settingsItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_exitItem);

        menu.Opening += (_, _) => RefreshText();
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
        var status = string.Format(LocalizationService.Get("TrayStatusFormat"), notes.Count, notes.Count(note => note.Visible));
        if (_header is not null) _header.Text = $"{LocalizationService.Get("AppName")}\n{status}";
        if (_notifyIcon is not null)
        {
            var tooltip = $"{LocalizationService.Get("AppName")}\n{LocalizationService.Get("TrayOpenHint")}";
            _notifyIcon.Text = tooltip.Length <= 63 ? tooltip : tooltip[..62] + "…";
        }
        if (_showAllItem is not null) _showAllItem.Enabled = notes.Count > 0;
        if (_hideAllItem is not null) _hideAllItem.Enabled = notes.Any(note => note.Visible);
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
            var elements = new StringInfo(title);
            var label = elements.LengthInTextElements > 26 ? elements.SubstringByTextElements(0, 26) + "…" : title;
            var noteItem = new Forms.ToolStripMenuItem(label.Replace("&", "&&"), null,
                (_, _) => _setNoteVisibility(note.Id, !note.Visible))
            {
                Checked = note.Visible,
                Tag = note.Id,
                Padding = new Forms.Padding(4, 6, 14, 6),
                AccessibleName = title,
                AccessibleDescription = LocalizationService.Get(note.Visible ? "TrayToggleHide" : "TrayToggleShow"),
                ToolTipText = $"{title}\n{LocalizationService.Get(note.Visible ? "TrayToggleHide" : "TrayToggleShow")}"
            };
            _noteListItem.DropDownItems.Add(noteItem);
        }
    }

    public void RefreshText()
    {
        if (_menu is not null && _fontFamily != LocalizationService.UiFontName)
        {
            var oldFont = _menuFont;
            var oldEmphasis = _emphasisFont;
            _fontFamily = LocalizationService.UiFontName;
            _menuFont = new Drawing.Font(_fontFamily, 9, Drawing.FontStyle.Regular);
            _emphasisFont = new Drawing.Font(_menuFont, Drawing.FontStyle.Bold);
            _menu.Font = _menuFont;
            if (_header is not null) _header.Font = _emphasisFont;
            if (_newNoteItem is not null) _newNoteItem.Font = _emphasisFont;
            oldEmphasis?.Dispose();
            oldFont?.Dispose();
        }
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

    public void ShowHotkeyWarning(string message) =>
        _notifyIcon?.ShowBalloonTip(6000, LocalizationService.Get("AppName"), message, Forms.ToolTipIcon.Warning);

    public void Dispose()
    {
        if (_notifyIcon is not null) _notifyIcon.Visible = false;
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        _menu?.Dispose();
        _menu = null;
        foreach (var image in _menuImages) image.Dispose();
        _menuImages.Clear();
        _emphasisFont?.Dispose();
        _menuFont?.Dispose();
        _emphasisFont = _menuFont = null;
        _ownedIcon?.Dispose();
        _ownedIcon = null;
    }

    internal static Drawing.Icon? LoadTrayIcon()
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/DeskStickyNotes;component/Assets/TrayIcon.ico"));
            if (info?.Stream is null)
            {
                return null;
            }

            using var stream = info.Stream;
            using var icon = new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
            return (Drawing.Icon)icon.Clone();
        }
        catch
        {
            return null;
        }
    }
}
