using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace DeskStickyNotes.Services;

public sealed class TrayService : IDisposable
{
    private readonly Action _newNote;
    private readonly Action _showAllNotes;
    private readonly Action _hideAllNotes;
    private readonly Action _showSettings;
    private readonly Action _exit;
    private Forms.NotifyIcon? _notifyIcon;
    private Drawing.Icon? _ownedIcon;
    private Forms.ToolStripMenuItem? _newNoteItem;
    private Forms.ToolStripMenuItem? _showAllItem;
    private Forms.ToolStripMenuItem? _hideAllItem;
    private Forms.ToolStripMenuItem? _settingsItem;
    private Forms.ToolStripMenuItem? _exitItem;

    public TrayService(
        Action newNote,
        Action showAllNotes,
        Action hideAllNotes,
        Action showSettings,
        Action exit)
    {
        _newNote = newNote;
        _showAllNotes = showAllNotes;
        _hideAllNotes = hideAllNotes;
        _showSettings = showSettings;
        _exit = exit;
    }

    public void Show()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        _newNoteItem = new Forms.ToolStripMenuItem("", null, (_, _) => _newNote());
        _showAllItem = new Forms.ToolStripMenuItem("", null, (_, _) => _showAllNotes());
        _hideAllItem = new Forms.ToolStripMenuItem("", null, (_, _) => _hideAllNotes());
        _settingsItem = new Forms.ToolStripMenuItem("", null, (_, _) => _showSettings());
        _exitItem = new Forms.ToolStripMenuItem("", null, (_, _) => _exit());

        menu.Items.Add(_newNoteItem);
        menu.Items.Add(_showAllItem);
        menu.Items.Add(_hideAllItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_settingsItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _ownedIcon = LoadAppIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = _ownedIcon ?? Drawing.SystemIcons.Application,
            Visible = true
        };
        _notifyIcon.MouseDoubleClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                _showAllNotes();
            }
        };
        RefreshText();
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
