using System.Windows.Input;
using DeskStickyNotes.Models;

namespace DeskStickyNotes.ViewModels;

public sealed class NoteViewModel : ObservableObject
{
    private readonly NoteModel _model;
    private double _fontSize;

    public NoteViewModel(NoteModel model, double fontSize)
    {
        _model = model;
        _model.Title = NoteModel.NormalizeTitle(_model.Title);
        _model.TextClips ??= [];
        _model.Color = NotePalette.Normalize(_model.Color);
        _model.BackgroundOpacity = NoteAppearance.NormalizeOpacity(_model.BackgroundOpacity);
        _model.TextColorMode = NoteTextColorMode.Normalize(_model.TextColorMode);
        _fontSize = fontSize;

        ToggleTopmostCommand = new RelayCommand(() => Topmost = !Topmost);
        NewNoteCommand = new RelayCommand(() => NewNoteRequested?.Invoke(this, EventArgs.Empty));
        HideCommand = new RelayCommand(() => HideRequested?.Invoke(this, EventArgs.Empty));
        DeleteCommand = new RelayCommand(() => DeleteRequested?.Invoke(this, EventArgs.Empty));
        SetColorCommand = new RelayCommand(parameter =>
        {
            if (parameter is string color)
            {
                Color = color;
            }
        });
        CycleColorCommand = new RelayCommand(CycleColor);
    }

    public event EventHandler? HideRequested;

    public event EventHandler? NewNoteRequested;

    public event EventHandler? DeleteRequested;

    public Guid Id => _model.Id;

    public string Title
    {
        get => _model.Title;
        set
        {
            var normalized = NoteModel.NormalizeTitle(value);
            if (_model.Title == normalized) return;
            _model.Title = normalized;
            Touch();
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<string> AvailableColors => NotePalette.Names;

    public string TextContent
    {
        get => _model.TextContent;
        set
        {
            if (_model.TextContent == value)
            {
                return;
            }

            _model.TextContent = value;
            Touch();
            OnPropertyChanged();
        }
    }

    public string RichContent
    {
        get => _model.RichContent;
        set
        {
            if (_model.RichContent == value)
            {
                return;
            }

            _model.RichContent = value;
            Touch();
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<TextClipModel> TextClips => _model.TextClips;

    public bool HasContent => !string.IsNullOrWhiteSpace(TextContent) || _model.TextClips.Count > 0;

    public void AddTextClip(TextClipModel clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        _model.TextClips.Add(clip);
        Touch();
        OnPropertyChanged(nameof(TextClips));
        OnPropertyChanged(nameof(HasContent));
    }

    public bool TryGetTextClip(Guid id, out TextClipModel? clip)
    {
        clip = _model.TextClips.FirstOrDefault(item => item.Id == id);
        return clip is not null;
    }

    public bool RemoveTextClip(Guid id)
    {
        var clip = _model.TextClips.FirstOrDefault(item => item.Id == id);
        if (clip is null)
        {
            return false;
        }

        _model.TextClips.Remove(clip);
        Touch();
        OnPropertyChanged(nameof(TextClips));
        OnPropertyChanged(nameof(HasContent));
        return true;
    }

    public void UpdateContent(string plainText, string richContent)
    {
        _model.TextContent = plainText;
        _model.RichContent = richContent;
        Touch();
        OnPropertyChanged(nameof(TextContent));
        OnPropertyChanged(nameof(RichContent));
        OnPropertyChanged(nameof(HasContent));
    }

    public double X
    {
        get => _model.X;
        set
        {
            if (Math.Abs(_model.X - value) < 0.1)
            {
                return;
            }

            _model.X = value;
            Touch();
            OnPropertyChanged();
        }
    }

    public double Y
    {
        get => _model.Y;
        set
        {
            if (Math.Abs(_model.Y - value) < 0.1)
            {
                return;
            }

            _model.Y = value;
            Touch();
            OnPropertyChanged();
        }
    }

    public double Width
    {
        get => _model.Width;
        set
        {
            var normalized = NormalizeWidth(value);
            if (Math.Abs(_model.Width - normalized) < 0.1)
            {
                return;
            }

            _model.Width = normalized;
            Touch();
            OnPropertyChanged();
        }
    }

    public double Height
    {
        get => _model.Height;
        set
        {
            var normalized = NormalizeHeight(value);
            if (Math.Abs(_model.Height - normalized) < 0.1)
            {
                return;
            }

            _model.Height = normalized;
            Touch();
            OnPropertyChanged();
        }
    }

    public string Color
    {
        get => _model.Color;
        set
        {
            var normalized = NotePalette.Normalize(value);
            if (_model.Color == normalized)
            {
                return;
            }

            _model.Color = normalized;
            Touch();
            OnPropertyChanged();
            OnPropertyChanged(nameof(BackgroundHex));
            OnPropertyChanged(nameof(HeaderHex));
            OnPropertyChanged(nameof(BorderHex));
            OnPropertyChanged(nameof(SwatchHex));
            OnPropertyChanged(nameof(TextForegroundHex));
            OnPropertyChanged(nameof(LinkForegroundHex));
        }
    }

    public double BackgroundOpacity
    {
        get => _model.BackgroundOpacity;
        set
        {
            var normalized = NoteAppearance.NormalizeOpacity(value);
            if (Math.Abs(_model.BackgroundOpacity - normalized) < 0.001)
            {
                return;
            }

            _model.BackgroundOpacity = normalized;
            Touch();
            OnPropertyChanged();
            OnPropertyChanged(nameof(BackgroundHex));
            OnPropertyChanged(nameof(HeaderHex));
            OnPropertyChanged(nameof(BorderHex));
        }
    }

    public string TextColorMode
    {
        get => _model.TextColorMode;
        set
        {
            var normalized = NoteTextColorMode.Normalize(value);
            if (_model.TextColorMode == normalized)
            {
                return;
            }

            _model.TextColorMode = normalized;
            Touch();
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextForegroundHex));
            OnPropertyChanged(nameof(LinkForegroundHex));
        }
    }

    public bool Topmost
    {
        get => _model.Topmost;
        set
        {
            if (_model.Topmost == value)
            {
                return;
            }

            _model.Topmost = value;
            Touch();
            OnPropertyChanged();
            OnPropertyChanged(nameof(TopmostLabel));
            OnPropertyChanged(nameof(TopmostGlyph));
        }
    }

    public bool Visible
    {
        get => _model.Visible;
        set
        {
            if (_model.Visible == value)
            {
                return;
            }

            _model.Visible = value;
            Touch();
            OnPropertyChanged();
        }
    }

    public double FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }

    public string BackgroundHex => NoteAppearance.WithOpacity(
        NotePalette.Get(Color).Background,
        BackgroundOpacity);

    public string HeaderHex => NoteAppearance.WithOpacity(
        NotePalette.Get(Color).Header,
        Math.Clamp(BackgroundOpacity + 0.18, 0.72, 1));

    public string BorderHex => NoteAppearance.WithOpacity(
        NotePalette.Get(Color).Border,
        Math.Clamp(BackgroundOpacity + 0.22, 0.58, 1));

    public string SwatchHex => NotePalette.Get(Color).Swatch;

    public string TextForegroundHex => NoteAppearance.ResolveTextColor(
        TextColorMode,
        NotePalette.Get(Color).Background);

    public string LinkForegroundHex => NoteAppearance.ResolveLinkColor(
        TextColorMode,
        NotePalette.Get(Color).Background);

    public string TopmostLabel => Topmost ? "Unpin" : "Pin";

    public string TopmostGlyph => Topmost ? "📌" : "📍";

    public DateTimeOffset UpdatedAt => _model.UpdatedAt;

    public ICommand ToggleTopmostCommand { get; }

    public ICommand NewNoteCommand { get; }

    public ICommand HideCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand SetColorCommand { get; }

    public ICommand CycleColorCommand { get; }

    public NoteModel ToModel()
    {
        return _model;
    }

    private void CycleColor()
    {
        var colors = AvailableColors;
        var index = -1;
        for (var i = 0; i < colors.Count; i++)
        {
            if (string.Equals(colors[i], Color, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        Color = colors[(index + 1) % colors.Count];
    }

    private void Touch()
    {
        _model.UpdatedAt = DateTimeOffset.UtcNow;
        OnPropertyChanged(nameof(UpdatedAt));
    }

    private double NormalizeWidth(double value)
    {
        return double.IsFinite(value)
            ? Math.Max(value, NoteModel.MinWidth)
            : NoteModel.DefaultWidth;
    }

    private double NormalizeHeight(double value)
    {
        if (!double.IsFinite(value))
        {
            return NoteModel.DefaultHeight;
        }

        return Math.Max(value, NoteModel.MinHeight);
    }
}
