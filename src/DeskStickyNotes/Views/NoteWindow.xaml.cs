using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using DeskStickyNotes.Models;
using DeskStickyNotes.Services;
using DeskStickyNotes.ViewModels;

namespace DeskStickyNotes.Views;

public partial class NoteWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExAppWindow = 0x00040000L;
    private const int WmSysCommand = 0x0112;
    private const int WmSize = 0x0005;
    private const int WmShowWindow = 0x0018;
    private const int WmWindowPosChanging = 0x0046;
    private const int WmDisplayChange = 0x007E;
    private const int WmDpiChanged = 0x02E0;
    private const int ScMinimize = 0xF020;
    private const int SizeMinimized = 1;
    private const int SwShownoactivate = 4;
    private const double ImageResizeStep = 1.12;
    private const double ImageMinWidth = 80;
    private const double ImageEditorMargin = 24;
    private const double ImageResizeHitArea = 9;
    private const string XamlPackagePrefix = "xamlpackage;base64,";
    private const string TextClipUriScheme = "desksticky";
    private const string TextClipUriHost = "text-clip";
    private const int MaximumFilesPerImport = 20;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpHideWindow = 0x0080;
    private const double CollapsedIconSize = 64;
    private static readonly IntPtr HwndTop = IntPtr.Zero;
    private static readonly IntPtr HwndTopmost = new(-1);

    private HwndSource? _hwndSource;
    private Popup? _openFlyout;
    private readonly TextFileImportService _textFileImportService = new();
    private readonly Dictionary<Guid, TextClipPreviewWindow> _textClipPreviewWindows = new();
    private readonly DispatcherTimer _showDesktopRecoveryTimer;
    private bool _allowClose;
    private bool _collapsedIconWasDragged;
    private bool _isIconCollapsed;
    private bool _isUpdatingWindowBounds;
    private bool _isApplyingBatchEdit;
    private bool _isLoadingEditor;
    private System.Windows.Controls.Image? _resizingImage;
    private System.Windows.Point _imageResizeStart;
    private double _imageResizeStartWidth;
    private bool _imageResizeChanged;
    private ImageResizeEdge _imageResizeEdge;
    private bool _isCollapsedIconMouseDown;
    private bool _isTitleMouseDown;
    private bool _isTaskbarMinimized;
    private Hyperlink? _pressedTextClipLink;
    private System.Windows.Point _textClipMouseDownPoint;
    private System.Windows.Point _collapsedIconMouseDownPoint;
    private double _expandedMinHeight;
    private double _expandedMinWidth;
    private System.Windows.Point _titleMouseDownPoint;
    private Rect _expandedBounds;
    private Rect? _lastRestoredBounds;
    private System.Windows.Point? _lastCollapsedIconPosition;
    private Thumb? _windowResizeThumb;
    private Rect _windowResizeStartBounds;
    private NativePoint _windowResizeStartPoint;
    private DpiScale _windowResizeDpi;
    private NoteResizeEdge _windowResizeEdge;
    private ResizeMode _expandedResizeMode;
    private int _showDesktopRecoveryAttempts;

    public NoteWindow(NoteViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PropertyChanged += NoteViewModel_PropertyChanged;
        UpdateNoteTitle();
        Width = viewModel.Width;
        Height = viewModel.Height;
        Editor.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(Hyperlink_RequestNavigate));
        Editor.AddHandler(System.Windows.DragDrop.PreviewDragEnterEvent, new System.Windows.DragEventHandler(Editor_PreviewDragEnter), true);
        Editor.AddHandler(System.Windows.DragDrop.PreviewDragOverEvent, new System.Windows.DragEventHandler(Editor_PreviewDragOver), true);
        Editor.AddHandler(System.Windows.DragDrop.PreviewDragLeaveEvent, new System.Windows.DragEventHandler(Editor_PreviewDragLeave), true);
        Editor.AddHandler(System.Windows.DragDrop.PreviewDropEvent, new System.Windows.DragEventHandler(Editor_PreviewDrop), true);
        System.Windows.DataObject.AddPastingHandler(Editor, Editor_Pasting);
        LoadEditorContent();

        _showDesktopRecoveryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _showDesktopRecoveryTimer.Tick += ShowDesktopRecoveryTimer_Tick;

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            FitWindowToCurrentWorkArea();
            _showDesktopRecoveryTimer.Start();
        };
        LocationChanged += (_, _) => UpdateGeometry();
        SizeChanged += (_, _) => UpdateGeometry();
        StateChanged += OnStateChanged;
        LocalizationService.LanguageChanged += LocalizationService_LanguageChanged;
    }

    private NoteViewModel ViewModel => (NoteViewModel)DataContext;

    public void CloseForApplication()
    {
        SaveEditorContent();
        PruneDetachedTextClips();
        _allowClose = true;
        Close();
    }

    public void MinimizeToTaskbar()
    {
        SaveEditorContent();
        _isTaskbarMinimized = true;
        ViewModel.Visible = true;
        ShowInTaskbar = true;
        ApplyTaskbarVisibility(showInTaskbar: true);
        WindowState = WindowState.Minimized;
    }

    public void HideFromDesktop()
    {
        CloseTextClipPreviews();
        _isTaskbarMinimized = false;
        WindowState = WindowState.Normal;
        ShowInTaskbar = false;
        ApplyTaskbarVisibility(showInTaskbar: false);
        Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose && !App.Current.IsExitRequested)
        {
            e.Cancel = true;
            ViewModel.HideCommand.Execute(null);
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        CloseOpenFlyout();
        TextFileDropOverlay.Visibility = Visibility.Collapsed;
        CloseTextClipPreviews();
        _showDesktopRecoveryTimer.Stop();
        LocalizationService.LanguageChanged -= LocalizationService_LanguageChanged;
        ViewModel.PropertyChanged -= NoteViewModel_PropertyChanged;
        _hwndSource?.RemoveHook(WndProc);
        base.OnClosed(e);
    }

    private void LocalizationService_LanguageChanged(object? sender, EventArgs e)
    {
        UpdateNoteTitle();
        ConfigureTextClipLinksInDocument();
    }

    private void NoteViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NoteViewModel.Title)) UpdateNoteTitle();
    }

    private void UpdateNoteTitle()
    {
        var title = string.IsNullOrEmpty(ViewModel.Title) ? LocalizationService.Get("NoteTitle") : ViewModel.Title;
        NoteTitleText.Text = title;
        NoteTitleText.ToolTip = $"{title}\n{LocalizationService.Get("RenameNoteHint")}";
        Title = $"{title} — {LocalizationService.Get("AppName")}";
        CollapsedIconContent.ToolTip = $"{title}\n{LocalizationService.Get("TipRestoreNote")}";
    }

    private void RenameNote_Click(object sender, RoutedEventArgs e) => RenameNote();

    private void RenameNote()
    {
        CloseOpenFlyout();
        var dialog = new RenameNoteWindow(ViewModel.Title) { Owner = this };
        if (dialog.ShowDialog() == true) ViewModel.Title = dialog.NoteTitle;
    }

    private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && !IsInsideButton(e.OriginalSource as DependencyObject))
        {
            _isTitleMouseDown = true;
            _titleMouseDownPoint = e.GetPosition(this);
            if (sender is UIElement element)
            {
                element.CaptureMouse();
            }

            e.Handled = true;
        }
    }

    private void DragBar_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isTitleMouseDown || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        var movedFarEnough =
            Math.Abs(position.X - _titleMouseDownPoint.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(position.Y - _titleMouseDownPoint.Y) >= SystemParameters.MinimumVerticalDragDistance;

        if (!movedFarEnough)
        {
            return;
        }

        _isTitleMouseDown = false;
        if (sender is UIElement element)
        {
            element.ReleaseMouseCapture();
        }

        if (WindowState == WindowState.Maximized)
        {
            RestoreMaximizedWindowForDrag(position);
        }

        try
        {
            DragMove();
        }
        catch
        {
            // DragMove can throw if mouse capture has already changed.
        }

        e.Handled = true;
    }

    private void DragBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isTitleMouseDown)
        {
            return;
        }

        _isTitleMouseDown = false;
        if (sender is UIElement element)
        {
            element.ReleaseMouseCapture();
        }

        CollapseToIcon();
        e.Handled = true;
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        CollapseToIcon();
        e.Handled = true;
    }

    private void CollapsedIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isCollapsedIconMouseDown)
        {
            return;
        }

        _isCollapsedIconMouseDown = false;
        if (sender is UIElement element)
        {
            element.ReleaseMouseCapture();
        }

        if (_collapsedIconWasDragged)
        {
            SaveCollapsedIconPosition();
        }
        else
        {
            RestoreFromIcon();
        }

        e.Handled = true;
    }

    private void CollapsedIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _isCollapsedIconMouseDown = true;
        _collapsedIconWasDragged = false;
        _collapsedIconMouseDownPoint = e.GetPosition(this);
        if (sender is UIElement element)
        {
            element.CaptureMouse();
        }

        e.Handled = true;
    }

    private void CollapsedIcon_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isCollapsedIconMouseDown || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        var movedFarEnough =
            Math.Abs(position.X - _collapsedIconMouseDownPoint.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(position.Y - _collapsedIconMouseDownPoint.Y) >= SystemParameters.MinimumVerticalDragDistance;

        if (!movedFarEnough)
        {
            return;
        }

        _collapsedIconWasDragged = true;
        _isCollapsedIconMouseDown = false;
        if (sender is UIElement element)
        {
            element.ReleaseMouseCapture();
        }

        try
        {
            DragMove();
            SaveCollapsedIconPosition();
        }
        catch
        {
            // DragMove can throw if mouse capture has already changed.
        }

        e.Handled = true;
    }

    private void CollapseToIcon()
    {
        if (_isIconCollapsed)
        {
            return;
        }

        SaveEditorContent();

        _expandedBounds = GetNormalWindowBounds();
        var iconPosition = _lastRestoredBounds is Rect restoredBounds
            && _lastCollapsedIconPosition is System.Windows.Point savedIconPosition
            && Math.Abs(_expandedBounds.Left - restoredBounds.Left) < 0.5
            && Math.Abs(_expandedBounds.Top - restoredBounds.Top) < 0.5
                ? savedIconPosition
                : _expandedBounds.TopLeft;
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }

        _expandedMinWidth = MinWidth;
        _expandedMinHeight = MinHeight;
        _expandedResizeMode = ResizeMode;

        if (!ViewModel.Topmost)
        {
            ViewModel.Topmost = true;
        }

        _isIconCollapsed = true;
        WindowResizeHandles.IsHitTestVisible = false;
        SetCollapsedChrome(collapsed: true);
        ExpandedContent.Visibility = Visibility.Collapsed;
        CollapsedIconContent.Visibility = Visibility.Visible;
        ResizeMode = ResizeMode.NoResize;
        MinWidth = CollapsedIconSize;
        MinHeight = CollapsedIconSize;
        Width = CollapsedIconSize;
        Height = CollapsedIconSize;
        ApplyWindowBounds(NoteWindowGeometry.FitToWorkArea(
            new Rect(iconPosition, new System.Windows.Size(CollapsedIconSize, CollapsedIconSize)), GetCurrentWorkArea()));
        CollapsedIconContent.Focus();
        ShowInTaskbar = false;
        ApplyTaskbarVisibility(showInTaskbar: false);
    }

    private void RestoreMaximizedWindowForDrag(System.Windows.Point titlePosition)
    {
        var restoredBounds = GetNormalWindowBounds();
        var maximizedWidth = Math.Max(1, ActualWidth);
        var horizontalRatio = Math.Clamp(titlePosition.X / maximizedWidth, 0, 1);
        var screenPosition = PointToScreen(titlePosition);
        var compositionTarget = PresentationSource.FromVisual(this)?.CompositionTarget;
        if (compositionTarget is not null)
        {
            screenPosition = compositionTarget.TransformFromDevice.Transform(screenPosition);
        }

        WindowState = WindowState.Normal;
        Width = restoredBounds.Width;
        Height = restoredBounds.Height;
        Left = screenPosition.X - (restoredBounds.Width * horizontalRatio);
        Top = screenPosition.Y - Math.Min(titlePosition.Y, 38);
        UpdateLayout();
    }

    private Rect GetNormalWindowBounds()
    {
        if (WindowState == WindowState.Maximized)
        {
            var restoredBounds = RestoreBounds;
            if (IsUsableWindowBounds(restoredBounds))
            {
                return restoredBounds;
            }
        }

        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        var bounds = new Rect(Left, Top, width, height);
        if (IsUsableWindowBounds(bounds))
        {
            return bounds;
        }

        return new Rect(
            ViewModel.X,
            ViewModel.Y,
            Math.Max(MinWidth, ViewModel.Width),
            Math.Max(MinHeight, ViewModel.Height));
    }

    private static bool IsUsableWindowBounds(Rect bounds)
    {
        return !bounds.IsEmpty
            && double.IsFinite(bounds.Left)
            && double.IsFinite(bounds.Top)
            && double.IsFinite(bounds.Width)
            && double.IsFinite(bounds.Height)
            && bounds.Width > 0
            && bounds.Height > 0;
    }

    private void RestoreFromIcon()
    {
        if (!_isIconCollapsed)
        {
            return;
        }

        var workArea = GetCurrentWorkArea();
        _lastCollapsedIconPosition = new System.Windows.Point(Left, Top);
        var minimumWidth = _expandedMinWidth > 0 ? _expandedMinWidth : NoteModel.MinWidth;
        var minimumHeight = _expandedMinHeight > 0 ? _expandedMinHeight : NoteModel.MinHeight;
        var bounds = NoteWindowGeometry.FitToWorkArea(new Rect(
            Left,
            Top,
            Math.Max(minimumWidth, _expandedBounds.Width),
            Math.Max(minimumHeight, _expandedBounds.Height)), workArea);

        MinWidth = Math.Min(minimumWidth, workArea.Width);
        MinHeight = Math.Min(minimumHeight, workArea.Height);
        ResizeMode = _expandedResizeMode;
        SetCollapsedChrome(collapsed: false);
        CollapsedIconContent.Visibility = Visibility.Collapsed;
        ExpandedContent.Visibility = Visibility.Visible;
        ApplyWindowBounds(bounds);
        _lastRestoredBounds = bounds;
        _isIconCollapsed = false;
        WindowResizeHandles.IsHitTestVisible = true;
        UpdateGeometry();
        Activate();
    }

    private Rect GetCurrentWorkArea()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var compositionTarget = PresentationSource.FromVisual(this)?.CompositionTarget;
        if (handle == IntPtr.Zero || compositionTarget is null)
        {
            return SystemParameters.WorkArea;
        }

        var workArea = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        var bounds = new Rect(workArea.Left, workArea.Top, workArea.Width, workArea.Height);
        bounds.Transform(compositionTarget.TransformFromDevice);
        return bounds;
    }

    private void ApplyWindowBounds(Rect bounds)
    {
        _isUpdatingWindowBounds = true;
        try
        {
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
        }
        finally
        {
            _isUpdatingWindowBounds = false;
        }

        UpdateGeometry(bounds);
    }

    private void FitWindowToCurrentWorkArea()
    {
        if (WindowState != WindowState.Normal || !IsLoaded)
        {
            return;
        }

        var workArea = GetCurrentWorkArea();
        MinWidth = Math.Min(_isIconCollapsed ? CollapsedIconSize : NoteModel.MinWidth, workArea.Width);
        MinHeight = Math.Min(_isIconCollapsed ? CollapsedIconSize : NoteModel.MinHeight, workArea.Height);
        var currentBounds = GetNormalWindowBounds();
        var fittedBounds = NoteWindowGeometry.FitToWorkArea(currentBounds, workArea);
        if (currentBounds != fittedBounds)
        {
            ApplyWindowBounds(fittedBounds);
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            RenameNote();
            e.Handled = true;
        }
        else if (_isIconCollapsed && e.Key is Key.Enter or Key.Space)
        {
            RestoreFromIcon();
            Editor.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _openFlyout is not null)
        {
            CloseOpenFlyout();
            e.Handled = true;
        }
    }

    private void WindowResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (!CanResizeFromWindowEdges()
            || sender is not Thumb thumb
            || !Enum.TryParse(thumb.Tag as string, out _windowResizeEdge)
            || !NativeMethods.GetCursorPos(out _windowResizeStartPoint))
        {
            (sender as Thumb)?.CancelDrag();
            return;
        }

        CloseOpenFlyout();
        _windowResizeStartBounds = GetNormalWindowBounds();
        _windowResizeDpi = VisualTreeHelper.GetDpi(this);
        _windowResizeThumb = thumb;
        e.Handled = true;
    }

    private void WindowResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!ReferenceEquals(sender, _windowResizeThumb)
            || !CanResizeFromWindowEdges()
            || !NativeMethods.GetCursorPos(out var point))
        {
            return;
        }

        // Screen coordinates remain stable when resizing from the top or left moves the window.
        var delta = new Vector(
            (point.X - _windowResizeStartPoint.X) / _windowResizeDpi.DpiScaleX,
            (point.Y - _windowResizeStartPoint.Y) / _windowResizeDpi.DpiScaleY);
        ApplyWindowBounds(NoteWindowGeometry.Resize(
            _windowResizeStartBounds, _windowResizeEdge, delta, new System.Windows.Size(MinWidth, MinHeight)));
        e.Handled = true;
    }

    private void WindowResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _windowResizeThumb = null;
        e.Handled = true;
    }

    private void SaveCollapsedIconPosition()
    {
        if (!double.IsFinite(Left) || !double.IsFinite(Top))
        {
            return;
        }

        _expandedBounds = new Rect(Left, Top, _expandedBounds.Width, _expandedBounds.Height);
        ViewModel.X = Left;
        ViewModel.Y = Top;
    }

    private void SetCollapsedChrome(bool collapsed)
    {
        if (collapsed)
        {
            WindowChrome.Background = System.Windows.Media.Brushes.Transparent;
            WindowChrome.Effect = null;
            return;
        }

        System.Windows.Data.BindingOperations.SetBinding(
            WindowChrome,
            Border.BackgroundProperty,
            new System.Windows.Data.Binding(nameof(NoteViewModel.BackgroundHex)));
        WindowChrome.Effect = WindowChromeShadow;
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
        {
            return;
        }

        var items = new List<UIElement>();
        items.Add(CreateFlyoutSectionHeader("AppearanceNoteColor"));
        foreach (var color in NotePalette.Names)
        {
            items.Add(CreateColorFlyoutButton(color));
        }

        items.Add(CreateFlyoutSeparator());
        items.Add(CreateFlyoutSectionHeader("AppearanceBackgroundOpacity"));
        items.Add(CreateOpacityControl());
        items.Add(CreateFlyoutSeparator());
        items.Add(CreateFlyoutSectionHeader("AppearanceTextColor"));
        foreach (var mode in NoteTextColorMode.Names)
        {
            items.Add(CreateTextColorFlyoutButton(mode));
        }

        ShowFlyout(button, items, 228);
    }

    private void EditorToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string action })
        {
            return;
        }

        Editor.Focus();

        switch (action)
        {
            case "Bold":
                EditingCommands.ToggleBold.Execute(null, Editor);
                break;
            case "Italic":
                EditingCommands.ToggleItalic.Execute(null, Editor);
                break;
            case "Underline":
                EditingCommands.ToggleUnderline.Execute(null, Editor);
                break;
            case "Strike":
                ToggleStrikethrough();
                break;
            case "Bullet":
                EditingCommands.ToggleBullets.Execute(null, Editor);
                break;
            case "Number":
                EditingCommands.ToggleNumbering.Execute(null, Editor);
                break;
            case "Todo":
                ToggleTodoLine();
                break;
            case "AlignLeft":
                EditingCommands.AlignLeft.Execute(null, Editor);
                break;
            case "AlignCenter":
                EditingCommands.AlignCenter.Execute(null, Editor);
                break;
            case "AlignRight":
                EditingCommands.AlignRight.Execute(null, Editor);
                break;
        }

        SaveEditorContent();
    }

    private void HeadingButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
        {
            return;
        }

        ShowFlyout(button, new[]
        {
            CreateHeadingFlyoutButton("StyleBody", "Body", 13, FontWeights.Normal),
            CreateHeadingFlyoutButton("StyleTitle", "Title", 15, FontWeights.Bold),
            CreateHeadingFlyoutButton("StyleHeading1", "Heading1", 14, FontWeights.Bold),
            CreateHeadingFlyoutButton("StyleHeading2", "Heading2", 13, FontWeights.SemiBold),
            CreateHeadingFlyoutButton("StyleHeading3", "Heading3", 13, FontWeights.Normal)
        }, 96);
    }

    private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        Editor.Focus();

        var selectedText = new TextRange(Editor.Selection.Start, Editor.Selection.End)
            .Text
            .TrimEnd('\r', '\n');
        var dialog = new HyperlinkWindow(selectedText)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var normalizedUrl = NormalizeUrl(dialog.LinkUrl);
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        var displayText = string.IsNullOrWhiteSpace(dialog.LinkText)
            ? normalizedUrl
            : dialog.LinkText;

        InsertHyperlink(displayText, uri);
        SaveEditorContent();
    }

    private void TextClipButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
        {
            return;
        }

        var selectedText = new TextRange(Editor.Selection.Start, Editor.Selection.End).Text;
        var clipboardText = TryGetClipboardText();

        var selectionButton = CreateFlyoutButton(LocalizationService.Get("TextClipMenuSelection"));
        selectionButton.IsEnabled = !string.IsNullOrWhiteSpace(selectedText);
        selectionButton.Click += (_, _) =>
        {
            CloseOpenFlyout();
            InsertTextAsClip(selectedText, replaceSelection: true);
        };

        var clipboardButton = CreateFlyoutButton(LocalizationService.Get("TextClipMenuClipboard"));
        clipboardButton.IsEnabled = !string.IsNullOrWhiteSpace(clipboardText);
        clipboardButton.Click += (_, _) =>
        {
            CloseOpenFlyout();
            InsertTextAsClip(clipboardText ?? "", replaceSelection: !Editor.Selection.IsEmpty);
        };

        var importButton = CreateFlyoutButton(LocalizationService.Get("TextClipMenuImportFile"));
        importButton.Click += (_, _) =>
        {
            CloseOpenFlyout();
            SelectAndImportTextFiles();
        };

        ShowFlyout(button, new UIElement[]
        {
            selectionButton,
            clipboardButton,
            CreateFlyoutSeparator(),
            importButton
        }, 224);
    }

    private void InsertTextAsClip(string content, bool replaceSelection)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        if (replaceSelection && !Editor.Selection.IsEmpty && SelectionIntersectsHyperlink())
        {
            ShowTextClipMessage(LocalizationService.Get("TextClipErrorSelectionContainsLink"));
            return;
        }

        var byteLength = Encoding.UTF8.GetByteCount(content);
        if (byteLength > TextFileImportService.MaximumFileSizeBytes)
        {
            ShowTextClipMessage(LocalizationService.Get("TextClipErrorFileTooLarge"));
            return;
        }

        if (!CanStoreTextClip(byteLength))
        {
            ShowTextClipMessage(LocalizationService.Get("TextClipErrorNoteLimit"));
            return;
        }

        var clip = new TextClipModel
        {
            Id = Guid.NewGuid(),
            Title = CreateTextClipTitle(content),
            Content = content,
            SourceFileName = "",
            FileExtension = ".txt",
            OriginalByteLength = byteLength,
            ImportedAt = DateTimeOffset.UtcNow
        };

        Editor.Focus();
        var removedSelection = replaceSelection && !Editor.Selection.IsEmpty;
        var inserted = false;
        _isApplyingBatchEdit = true;
        Editor.BeginChange();
        try
        {
            if (removedSelection)
            {
                Editor.Selection.Text = string.Empty;
            }

            inserted = TryInsertTextClipLink(clip);
            if (inserted)
            {
                ViewModel.AddTextClip(clip);
            }
        }
        finally
        {
            Editor.EndChange();
            if (!inserted && removedSelection && Editor.CanUndo)
            {
                Editor.Undo();
            }

            _isApplyingBatchEdit = false;
        }

        if (!inserted)
        {
            ShowTextClipMessage(LocalizationService.Get("TextClipErrorInsertFailed"));
            return;
        }

        SaveEditorContent();
    }

    private async void SelectAndImportTextFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocalizationService.Get("TextClipImportDialogTitle"),
            Filter = CreateTextFileDialogFilter(),
            Multiselect = true,
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await ImportTextFilesAsync(dialog.FileNames);
        }
    }

    private static string CreateTextFileDialogFilter()
    {
        var extensionPatterns = TextFileImportService.SupportedExtensions
                .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
                .Select(extension => $"*{extension}");
        var namedFilePatterns = TextFileImportService.SupportedFileNames
            .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase);
        var patterns = string.Join(';', extensionPatterns.Concat(namedFilePatterns));
        return $"{LocalizationService.Get("TextClipImportFilterName")}|{patterns}|"
            + $"{LocalizationService.Get("TextClipImportAllFiles")}|*.*";
    }

    private static string? TryGetClipboardText()
    {
        try
        {
            return System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText)
                ? System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string CreateTextClipTitle(string content)
    {
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                continue;
            }

            var title = string.Join(" ", words);
            return title.Length <= 52 ? title : title[..49] + "…";
        }

        return LocalizationService.Get("TextClipDefaultTitle");
    }

    private void ShowFlyout(System.Windows.Controls.Button owner, IEnumerable<UIElement> items, double minWidth)
    {
        CloseOpenFlyout();

        var panel = new StackPanel
        {
            MinWidth = minWidth
        };
        KeyboardNavigation.SetTabNavigation(panel, KeyboardNavigationMode.Cycle);
        panel.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            CloseOpenFlyout();
            owner.Focus();
            e.Handled = true;
        };

        foreach (var item in items)
        {
            panel.Children.Add(item);
        }

        var root = new Border
        {
            Style = (Style)FindResource("FlyoutMenuBorderStyle"),
            Child = panel
        };

        var popup = new Popup
        {
            AllowsTransparency = true,
            Child = root,
            Placement = PlacementMode.Bottom,
            PlacementTarget = owner,
            PopupAnimation = PopupAnimation.Fade,
            StaysOpen = false,
            VerticalOffset = 4
        };

        popup.Closed += (_, _) =>
        {
            if (ReferenceEquals(_openFlyout, popup))
            {
                _openFlyout = null;
            }
        };
        popup.Opened += (_, _) => panel.Children
            .OfType<System.Windows.Controls.Button>()
            .FirstOrDefault(item => item.IsEnabled)
            ?.Focus();

        _openFlyout = popup;
        popup.IsOpen = true;
    }

    private void CloseOpenFlyout()
    {
        var popup = _openFlyout;
        _openFlyout = null;
        if (popup is not null)
        {
            popup.IsOpen = false;
        }
    }

    private System.Windows.Controls.Button CreateFlyoutButton(object content)
    {
        return new System.Windows.Controls.Button
        {
            Style = (Style)FindResource("FlyoutMenuButtonStyle"),
            Content = content
        };
    }

    private UIElement CreateFlyoutSectionHeader(string resourceKey)
    {
        return new TextBlock
        {
            Text = Services.LocalizationService.Get(resourceKey),
            Margin = new Thickness(9, 5, 9, 3),
            Foreground = ToBrush("#667085"),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold
        };
    }

    private static UIElement CreateFlyoutSeparator()
    {
        return new Border
        {
            Height = 1,
            Margin = new Thickness(7, 5, 7, 4),
            Background = ToBrush("#24000000")
        };
    }

    private UIElement CreateOpacityControl()
    {
        var panel = new Grid
        {
            Margin = new Thickness(9, 0, 9, 5)
        };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var valueLabel = new TextBlock
        {
            Text = $"{Math.Round(ViewModel.BackgroundOpacity * 100):0}%",
            Margin = new Thickness(0, 0, 0, 2),
            Foreground = ToBrush("#475467"),
            FontSize = 12,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        Grid.SetRow(valueLabel, 0);
        panel.Children.Add(valueLabel);

        var slider = new Slider
        {
            Minimum = NoteAppearance.MinBackgroundOpacity * 100,
            Maximum = NoteAppearance.MaxBackgroundOpacity * 100,
            Value = ViewModel.BackgroundOpacity * 100,
            TickFrequency = 5,
            IsSnapToTickEnabled = false,
            SmallChange = 1,
            LargeChange = 10,
            ToolTip = Services.LocalizationService.Get("AppearanceBackgroundOpacity")
        };
        slider.ValueChanged += (_, e) =>
        {
            ViewModel.BackgroundOpacity = e.NewValue / 100;
            valueLabel.Text = $"{Math.Round(e.NewValue):0}%";
        };
        Grid.SetRow(slider, 1);
        panel.Children.Add(slider);
        return panel;
    }

    private System.Windows.Controls.Button CreateColorFlyoutButton(string color)
    {
        var palette = NotePalette.Get(color);
        var isSelected = string.Equals(ViewModel.Color, color, StringComparison.OrdinalIgnoreCase);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var swatch = new Border
        {
            Width = 13,
            Height = 13,
            Margin = new Thickness(0, 0, 8, 0),
            CornerRadius = new CornerRadius(6.5),
            Background = ToBrush(palette.Swatch),
            BorderBrush = ToBrush(palette.Border),
            BorderThickness = new Thickness(1)
        };
        Grid.SetColumn(swatch, 0);
        grid.Children.Add(swatch);

        var label = new TextBlock
        {
            Text = Services.LocalizationService.GetColorName(color),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        var check = new TextBlock
        {
            Text = isSelected ? "✓" : "",
            Width = 16,
            Foreground = ToBrush("#2563EB"),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
        Grid.SetColumn(check, 2);
        grid.Children.Add(check);

        var button = CreateFlyoutButton(grid);
        button.Click += (_, _) =>
        {
            ViewModel.SetColorCommand.Execute(color);
            ApplyTextColorToDocument();
            CloseOpenFlyout();
        };

        return button;
    }

    private System.Windows.Controls.Button CreateTextColorFlyoutButton(string mode)
    {
        var normalizedMode = NoteTextColorMode.Normalize(mode);
        var isSelected = string.Equals(ViewModel.TextColorMode, normalizedMode, StringComparison.OrdinalIgnoreCase);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var swatch = new Border
        {
            Width = 15,
            Height = 15,
            Margin = new Thickness(0, 0, 8, 0),
            CornerRadius = new CornerRadius(7.5),
            Background = CreateTextColorSwatch(normalizedMode),
            BorderBrush = ToBrush("#66000000"),
            BorderThickness = new Thickness(1)
        };
        Grid.SetColumn(swatch, 0);
        grid.Children.Add(swatch);

        var resourceKey = normalizedMode switch
        {
            NoteTextColorMode.Dark => "TextColorDark",
            NoteTextColorMode.Light => "TextColorLight",
            _ => "TextColorAuto"
        };
        var label = new TextBlock
        {
            Text = Services.LocalizationService.Get(resourceKey),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        var check = new TextBlock
        {
            Text = isSelected ? "✓" : "",
            Width = 16,
            Foreground = ToBrush("#2563EB"),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
        Grid.SetColumn(check, 2);
        grid.Children.Add(check);

        var button = CreateFlyoutButton(grid);
        button.Click += (_, _) =>
        {
            ViewModel.TextColorMode = normalizedMode;
            ApplyTextColorToDocument();
            CloseOpenFlyout();
        };
        return button;
    }

    private System.Windows.Media.Brush CreateTextColorSwatch(string mode)
    {
        if (mode == NoteTextColorMode.Dark)
        {
            return ToBrush(NoteAppearance.DarkText);
        }

        if (mode == NoteTextColorMode.Light)
        {
            return ToBrush(NoteAppearance.LightText);
        }

        return new LinearGradientBrush(
            ToColor(NoteAppearance.DarkText),
            ToColor(NoteAppearance.LightText),
            new System.Windows.Point(0, 0),
            new System.Windows.Point(1, 1));
    }

    private System.Windows.Controls.Button CreateHeadingFlyoutButton(string resourceKey, string style, double fontSize, FontWeight fontWeight)
    {
        var label = new TextBlock
        {
            Text = Services.LocalizationService.Get(resourceKey),
            FontSize = fontSize,
            FontWeight = fontWeight,
            VerticalAlignment = VerticalAlignment.Center
        };

        var button = CreateFlyoutButton(label);
        button.Click += (_, _) =>
        {
            CloseOpenFlyout();
            Editor.Focus();
            ApplyHeadingStyle(style);
            SaveEditorContent();
        };

        return button;
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isLoadingEditor && !_isApplyingBatchEdit)
        {
            SaveEditorContent();
        }
    }

    private void Editor_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter
            && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
            && TryGetTextClipAtPosition(Editor.CaretPosition, out var clipId))
        {
            e.Handled = true;
            ShowTextClipPreview(clipId);
            return;
        }

        if (e.Key != Key.V
            || (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift))
                != (ModifierKeys.Control | ModifierKeys.Shift))
        {
            return;
        }

        var clipboardText = TryGetClipboardText();
        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            return;
        }

        e.Handled = true;
        InsertTextAsClip(clipboardText, replaceSelection: !Editor.Selection.IsEmpty);
    }

    private static bool TryGetTextClipAtPosition(TextPointer position, out Guid clipId)
    {
        clipId = Guid.Empty;
        var current = position.Parent as DependencyObject;
        while (current is not null)
        {
            if (current is Hyperlink hyperlink)
            {
                return TryGetTextClipId(hyperlink.NavigateUri, out clipId);
            }

            current = LogicalTreeHelper.GetParent(current);
        }

        return false;
    }

    private bool SelectionIntersectsHyperlink()
    {
        var selectionStart = Editor.Selection.Start;
        var selectionEnd = Editor.Selection.End;
        return FindLogicalTextElements(Editor.Document)
            .OfType<Hyperlink>()
            .Any(hyperlink => selectionStart.CompareTo(hyperlink.ElementEnd) < 0
                              && selectionEnd.CompareTo(hyperlink.ElementStart) > 0);
    }

    private void Editor_PreviewDragEnter(object sender, System.Windows.DragEventArgs e)
    {
        UpdateTextFileDragState(e);
    }

    private void Editor_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        UpdateTextFileDragState(e);
    }

    private void Editor_PreviewDragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (!TryGetFilePaths(e.Data, out _))
        {
            return;
        }

        TextFileDropOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;
    }

    private async void Editor_PreviewDrop(object sender, System.Windows.DragEventArgs e)
    {
        TextFileDropOverlay.Visibility = Visibility.Collapsed;
        if (!TryGetFilePaths(e.Data, out var paths))
        {
            return;
        }

        e.Handled = true;
        var insertionPosition = Editor.GetPositionFromPoint(e.GetPosition(Editor), snapToText: true);
        await ImportTextFilesAsync(paths, insertionPosition);
    }

    private void UpdateTextFileDragState(System.Windows.DragEventArgs e)
    {
        if (!TryGetFilePaths(e.Data, out var paths))
        {
            TextFileDropOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        var canImport = paths.Any(TextFileImportService.IsSupportedFile);
        e.Effects = canImport ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        TextFileDropOverlay.Visibility = canImport ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private async Task ImportTextFilesAsync(IReadOnlyList<string> filePaths, TextPointer? insertionPosition = null)
    {
        var paths = filePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (paths.Count == 0)
        {
            return;
        }

        var targetPosition = insertionPosition
            ?? Editor.Selection.Start.GetInsertionPosition(LogicalDirection.Forward)
            ?? Editor.CaretPosition;

        List<(string Path, TextFileImportResult Result)> importResults;
        try
        {
            importResults = await Task.Run(() => paths
                .Take(MaximumFilesPerImport)
                .Select(path => (Path: path, Result: _textFileImportService.Import(path)))
                .ToList());
        }
        catch
        {
            ShowTextClipMessage(LocalizationService.Get("TextClipErrorReadFailed"));
            return;
        }

        if (_allowClose || !IsLoaded)
        {
            return;
        }

        Editor.Selection.Select(targetPosition, targetPosition);
        Editor.CaretPosition = targetPosition;

        Editor.Focus();
        var errors = new List<string>();
        var importedCount = 0;

        _isApplyingBatchEdit = true;
        try
        {
            foreach (var importedFile in importResults)
            {
                var path = importedFile.Path;
                var result = importedFile.Result;
                if (!result.IsSuccess)
                {
                    errors.Add(FormatTextFileImportError(path, result.Error));
                    continue;
                }

                var clip = result.Clip!;
                var storedSize = GetTextClipStoredSize(clip);
                if (!CanStoreTextClip(storedSize))
                {
                    errors.Add($"{GetSafeFileName(path)} — {LocalizationService.Get("TextClipErrorNoteLimit")}");
                    continue;
                }

                if (!TryInsertTextClipLink(clip))
                {
                    errors.Add($"{GetSafeFileName(path)} — {LocalizationService.Get("TextClipErrorInsertFailed")}");
                    continue;
                }

                ViewModel.AddTextClip(clip);
                importedCount++;
            }
        }
        finally
        {
            _isApplyingBatchEdit = false;
        }

        if (paths.Count > MaximumFilesPerImport)
        {
            errors.Add(string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                LocalizationService.Get("TextClipErrorTooManyFiles"),
                MaximumFilesPerImport));
        }

        if (importedCount > 0)
        {
            SaveEditorContent();
        }

        if (errors.Count > 0)
        {
            ShowTextFileImportErrors(errors);
        }
    }

    private bool CanStoreTextClip(long additionalBytes)
    {
        var attachedClipIds = GetAttachedTextClipIds();
        var storedBytes = ViewModel.TextClips
            .Where(clip => attachedClipIds.Contains(clip.Id))
            .Sum(GetTextClipStoredSize);
        return additionalBytes >= 0
            && ViewModel.TextClips.Count(clip => attachedClipIds.Contains(clip.Id)) < NoteModel.MaximumTextClipCount
            && storedBytes <= NoteModel.MaximumTextClipBytes - additionalBytes;
    }

    private static long GetTextClipStoredSize(TextClipModel clip)
    {
        return Math.Max(clip.OriginalByteLength, Encoding.UTF8.GetByteCount(clip.Content ?? ""));
    }

    private HashSet<Guid> GetAttachedTextClipIds()
    {
        return FindLogicalTextElements(Editor.Document)
            .OfType<Hyperlink>()
            .Select(hyperlink => TryGetTextClipId(hyperlink.NavigateUri, out var clipId) ? clipId : Guid.Empty)
            .Where(clipId => clipId != Guid.Empty)
            .ToHashSet();
    }

    private void PruneDetachedTextClips()
    {
        var attachedClipIds = GetAttachedTextClipIds();
        foreach (var clipId in ViewModel.TextClips
                     .Select(clip => clip.Id)
                     .Where(clipId => !attachedClipIds.Contains(clipId))
                     .ToList())
        {
            ViewModel.RemoveTextClip(clipId);
        }
    }

    private static bool TryGetFilePaths(System.Windows.IDataObject dataObject, out string[] paths)
    {
        paths = [];
        try
        {
            if (!dataObject.GetDataPresent(System.Windows.DataFormats.FileDrop)
                || dataObject.GetData(System.Windows.DataFormats.FileDrop) is not string[] droppedPaths)
            {
                return false;
            }

            paths = droppedPaths;
            return paths.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatTextFileImportError(string path, TextFileImportError error)
    {
        var resourceKey = error switch
        {
            TextFileImportError.InvalidPath => "TextClipErrorInvalidPath",
            TextFileImportError.FileNotFound => "TextClipErrorFileNotFound",
            TextFileImportError.UnsupportedFileType => "TextClipErrorUnsupportedFileType",
            TextFileImportError.FileTooLarge => "TextClipErrorFileTooLarge",
            TextFileImportError.AccessDenied => "TextClipErrorAccessDenied",
            TextFileImportError.UnsupportedEncoding => "TextClipErrorUnsupportedEncoding",
            TextFileImportError.BinaryContent => "TextClipErrorBinaryContent",
            _ => "TextClipErrorReadFailed"
        };
        return $"{GetSafeFileName(path)} — {LocalizationService.Get(resourceKey)}";
    }

    private static string GetSafeFileName(string path)
    {
        try
        {
            return Path.GetFileName(path);
        }
        catch
        {
            return path;
        }
    }

    private void ShowTextFileImportErrors(IReadOnlyList<string> errors)
    {
        var visibleErrors = errors.Take(8).Select(error => $"• {error}").ToList();
        if (errors.Count > visibleErrors.Count)
        {
            visibleErrors.Add(string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                LocalizationService.Get("TextClipErrorMoreFiles"),
                errors.Count - visibleErrors.Count));
        }

        var message = LocalizationService.Get("TextClipImportErrorHeader")
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(Environment.NewLine, visibleErrors);
        ShowTextClipMessage(message);
    }

    private void ShowTextClipMessage(string message, string titleResourceKey = "TextClipImportErrorTitle")
    {
        System.Windows.MessageBox.Show(
            this,
            message,
            LocalizationService.Get(titleResourceKey),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void Editor_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (TryGetFilePaths(e.SourceDataObject, out var paths))
        {
            e.CancelCommand();
            await ImportTextFilesAsync(paths);
            return;
        }

        var bitmap = GetPastedBitmap(e.SourceDataObject);
        if (bitmap is null)
        {
            return;
        }

        e.CancelCommand();
        InsertImage(bitmap);
        SaveEditorContent();
    }

    private void LoadEditorContent()
    {
        _isLoadingEditor = true;
        try
        {
            Editor.Document.Blocks.Clear();

            if (!string.IsNullOrWhiteSpace(ViewModel.RichContent))
            {
                var format = System.Windows.DataFormats.Xaml;
                using var stream = CreateRichContentStream(ViewModel.RichContent, ref format);
                new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Load(stream, format);
                Dispatcher.BeginInvoke(() =>
                {
                    ConfigureImagesInDocument();
                    ConfigureTextClipLinksInDocument();
                }, DispatcherPriority.Loaded);
                return;
            }

            Editor.Document.Blocks.Add(new Paragraph(new Run(ViewModel.TextContent)));
        }
        catch
        {
            Editor.Document.Blocks.Clear();
            Editor.Document.Blocks.Add(new Paragraph(new Run(ViewModel.TextContent)));
        }
        finally
        {
            ApplyTextColorToDocument(save: false);
            _isLoadingEditor = false;
        }
    }

    private void ApplyTextColorToDocument(bool save = true)
    {
        var textBrush = ToBrush(ViewModel.TextForegroundHex);
        var linkBrush = ToBrush(ViewModel.LinkForegroundHex);
        Editor.Document.Foreground = textBrush;

        foreach (var element in FindLogicalTextElements(Editor.Document))
        {
            if (element is Hyperlink)
            {
                element.Foreground = linkBrush;
            }
            else
            {
                element.ClearValue(TextElement.ForegroundProperty);
            }
        }

        if (save && !_isLoadingEditor)
        {
            SaveEditorContent();
        }
    }

    private static IEnumerable<TextElement> FindLogicalTextElements(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is not TextElement textElement)
            {
                continue;
            }

            yield return textElement;
            foreach (var descendant in FindLogicalTextElements(textElement))
            {
                yield return descendant;
            }
        }
    }

    private void SaveEditorContent()
    {
        var range = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd);
        using var stream = new MemoryStream();
        range.Save(stream, System.Windows.DataFormats.XamlPackage);

        var plainText = range.Text.TrimEnd('\r', '\n');
        var richContent = XamlPackagePrefix + Convert.ToBase64String(stream.ToArray());
        ViewModel.UpdateContent(plainText, richContent);
    }

    private static MemoryStream CreateRichContentStream(string richContent, ref string format)
    {
        if (richContent.StartsWith(XamlPackagePrefix, StringComparison.Ordinal))
        {
            format = System.Windows.DataFormats.XamlPackage;
            return new MemoryStream(Convert.FromBase64String(richContent[XamlPackagePrefix.Length..]));
        }

        format = System.Windows.DataFormats.Xaml;
        return new MemoryStream(Encoding.UTF8.GetBytes(richContent));
    }

    private static BitmapSource? GetPastedBitmap(System.Windows.IDataObject dataObject)
    {
        try
        {
            if (dataObject.GetDataPresent(System.Windows.DataFormats.Bitmap)
                && dataObject.GetData(System.Windows.DataFormats.Bitmap) is BitmapSource bitmap)
            {
                return bitmap;
            }

            return System.Windows.Clipboard.ContainsImage() ? System.Windows.Clipboard.GetImage() : null;
        }
        catch
        {
            return null;
        }
    }

    private void InsertImage(BitmapSource bitmap)
    {
        var image = CreateEditableImage(bitmap);
        var insertionPosition = Editor.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
        var container = new InlineUIContainer(image, insertionPosition);
        Editor.CaretPosition = container.ElementEnd;
        Editor.CaretPosition.InsertTextInRun(" ");
    }

    private System.Windows.Controls.Image CreateEditableImage(BitmapSource bitmap)
    {
        var source = bitmap;
        if (source.CanFreeze)
        {
            source.Freeze();
        }

        var image = new System.Windows.Controls.Image
        {
            Source = source,
            Width = GetInitialImageWidth(source),
            Stretch = Stretch.Uniform
        };

        ConfigureImage(image);
        return image;
    }

    private void ConfigureImage(System.Windows.Controls.Image image)
    {
        image.Cursor = System.Windows.Input.Cursors.Arrow;
        image.ToolTip = DeskStickyNotes.Services.LocalizationService.Get("TipResizeImage");
        image.PreviewMouseLeftButtonDown -= Image_PreviewMouseLeftButtonDown;
        image.PreviewMouseLeftButtonDown += Image_PreviewMouseLeftButtonDown;
        image.PreviewMouseMove -= Image_PreviewMouseMove;
        image.PreviewMouseMove += Image_PreviewMouseMove;
        image.MouseLeave -= Image_MouseLeave;
        image.MouseLeave += Image_MouseLeave;
        image.PreviewMouseLeftButtonUp -= Image_PreviewMouseLeftButtonUp;
        image.PreviewMouseLeftButtonUp += Image_PreviewMouseLeftButtonUp;
        image.LostMouseCapture -= Image_LostMouseCapture;
        image.LostMouseCapture += Image_LostMouseCapture;
        image.PreviewMouseWheel -= Image_PreviewMouseWheel;
        image.PreviewMouseWheel += Image_PreviewMouseWheel;
        image.ContextMenu = CreateImageContextMenu(image);
    }

    private ContextMenu CreateImageContextMenu(System.Windows.Controls.Image image)
    {
        var menu = new ContextMenu();
        var smaller = new MenuItem { Header = DeskStickyNotes.Services.LocalizationService.Get("ImageMenuSmaller") };
        smaller.Click += (_, _) => ResizeImage(image, 1 / ImageResizeStep);

        var larger = new MenuItem { Header = DeskStickyNotes.Services.LocalizationService.Get("ImageMenuLarger") };
        larger.Click += (_, _) => ResizeImage(image, ImageResizeStep);

        var fit = new MenuItem { Header = DeskStickyNotes.Services.LocalizationService.Get("ImageMenuFitWidth") };
        fit.Click += (_, _) => FitImageToEditor(image);

        menu.Items.Add(smaller);
        menu.Items.Add(larger);
        menu.Items.Add(fit);
        return menu;
    }

    private void Image_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not System.Windows.Controls.Image image || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        ResizeImage(image, e.Delta > 0 ? ImageResizeStep : 1 / ImageResizeStep);
        e.Handled = true;
    }

    private void Image_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Image image || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var edge = GetImageResizeEdge(image, e.GetPosition(image));
        if (edge == ImageResizeEdge.None)
        {
            return;
        }

        _resizingImage = image;
        _imageResizeEdge = edge;
        _imageResizeStart = e.GetPosition(Editor);
        _imageResizeStartWidth = GetImageWidth(image);
        _imageResizeChanged = false;
        image.CaptureMouse();
        e.Handled = true;
    }

    private void Image_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.Image image)
        {
            return;
        }

        if (!ReferenceEquals(_resizingImage, image) || !image.IsMouseCaptured)
        {
            UpdateImageResizeCursor(image, e.GetPosition(image));
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            FinishImageResize(image);
            return;
        }

        var currentPosition = e.GetPosition(Editor);
        var deltaX = currentPosition.X - _imageResizeStart.X;
        var deltaY = currentPosition.Y - _imageResizeStart.Y;
        var delta = GetImageWidthDelta(image, _imageResizeEdge, deltaX, deltaY);
        var maxWidth = Math.Max(ImageMinWidth, GetMaxImageWidth());
        var width = Math.Clamp(_imageResizeStartWidth + delta, ImageMinWidth, maxWidth);
        if (Math.Abs(width - GetImageWidth(image)) >= 0.5)
        {
            image.Width = width;
            _imageResizeChanged = true;
        }

        e.Handled = true;
    }

    private void Image_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Image image && !ReferenceEquals(_resizingImage, image))
        {
            image.Cursor = System.Windows.Input.Cursors.Arrow;
        }
    }

    private void Image_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Image image || !ReferenceEquals(_resizingImage, image))
        {
            return;
        }

        FinishImageResize(image);
        e.Handled = true;
    }

    private void Image_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.Image image || !ReferenceEquals(_resizingImage, image))
        {
            return;
        }

        var changed = _imageResizeChanged;
        ResetImageResizeState();
        if (changed)
        {
            SaveEditorContent();
        }
    }

    private void FinishImageResize(System.Windows.Controls.Image image)
    {
        var changed = _imageResizeChanged;
        ResetImageResizeState();
        if (image.IsMouseCaptured)
        {
            image.ReleaseMouseCapture();
        }

        if (changed)
        {
            SaveEditorContent();
        }
    }

    private void ResetImageResizeState()
    {
        _resizingImage = null;
        _imageResizeEdge = ImageResizeEdge.None;
        _imageResizeStartWidth = 0;
        _imageResizeChanged = false;
    }

    private static ImageResizeEdge GetImageResizeEdge(
        System.Windows.Controls.Image image,
        System.Windows.Point position)
    {
        if (image.ActualWidth <= 0 || image.ActualHeight <= 0)
        {
            return ImageResizeEdge.None;
        }

        var hitArea = Math.Min(ImageResizeHitArea, Math.Min(image.ActualWidth, image.ActualHeight) / 3);
        var left = position.X <= hitArea;
        var right = position.X >= image.ActualWidth - hitArea;
        var top = position.Y <= hitArea;
        var bottom = position.Y >= image.ActualHeight - hitArea;

        if (top && left) return ImageResizeEdge.TopLeft;
        if (top && right) return ImageResizeEdge.TopRight;
        if (bottom && left) return ImageResizeEdge.BottomLeft;
        if (bottom && right) return ImageResizeEdge.BottomRight;
        if (left) return ImageResizeEdge.Left;
        if (right) return ImageResizeEdge.Right;
        if (top) return ImageResizeEdge.Top;
        if (bottom) return ImageResizeEdge.Bottom;
        return ImageResizeEdge.None;
    }

    private static void UpdateImageResizeCursor(
        System.Windows.Controls.Image image,
        System.Windows.Point position)
    {
        image.Cursor = GetImageResizeEdge(image, position) switch
        {
            ImageResizeEdge.Left or ImageResizeEdge.Right => System.Windows.Input.Cursors.SizeWE,
            ImageResizeEdge.Top or ImageResizeEdge.Bottom => System.Windows.Input.Cursors.SizeNS,
            ImageResizeEdge.TopLeft or ImageResizeEdge.BottomRight => System.Windows.Input.Cursors.SizeNWSE,
            ImageResizeEdge.TopRight or ImageResizeEdge.BottomLeft => System.Windows.Input.Cursors.SizeNESW,
            _ => System.Windows.Input.Cursors.Arrow
        };
    }

    private static double GetImageWidthDelta(
        System.Windows.Controls.Image image,
        ImageResizeEdge edge,
        double deltaX,
        double deltaY)
    {
        var aspectRatio = image.ActualHeight > 0
            ? GetImageWidth(image) / image.ActualHeight
            : 1;
        var horizontalDelta = edge is ImageResizeEdge.Left or ImageResizeEdge.TopLeft or ImageResizeEdge.BottomLeft
            ? -deltaX
            : deltaX;
        var verticalDelta = edge is ImageResizeEdge.Top or ImageResizeEdge.TopLeft or ImageResizeEdge.TopRight
            ? -deltaY * aspectRatio
            : deltaY * aspectRatio;

        return edge switch
        {
            ImageResizeEdge.Left or ImageResizeEdge.Right => horizontalDelta,
            ImageResizeEdge.Top or ImageResizeEdge.Bottom => verticalDelta,
            _ => Math.Abs(horizontalDelta) >= Math.Abs(verticalDelta) ? horizontalDelta : verticalDelta
        };
    }

    private void ResizeImage(System.Windows.Controls.Image image, double scale)
    {
        var currentWidth = GetImageWidth(image);
        var maxWidth = GetMaxImageWidth();
        image.Width = Math.Clamp(currentWidth * scale, ImageMinWidth, Math.Max(ImageMinWidth, maxWidth));
        SaveEditorContent();
    }

    private void FitImageToEditor(System.Windows.Controls.Image image)
    {
        image.Width = GetMaxImageWidth();
        SaveEditorContent();
    }

    private double GetInitialImageWidth(BitmapSource source)
    {
        var naturalWidth = source.PixelWidth * 96.0 / Math.Max(1, source.DpiX);
        return Math.Min(naturalWidth, GetMaxImageWidth());
    }

    private double GetMaxImageWidth()
    {
        var width = Editor.ActualWidth - Editor.Padding.Left - Editor.Padding.Right - ImageEditorMargin;
        return Math.Max(ImageMinWidth, double.IsFinite(width) && width > 0 ? width : 320);
    }

    private static double GetImageWidth(System.Windows.Controls.Image image)
    {
        if (double.IsFinite(image.Width) && image.Width > 0)
        {
            return image.Width;
        }

        if (image.ActualWidth > 0)
        {
            return image.ActualWidth;
        }

        return image.Source is BitmapSource source
            ? source.PixelWidth * 96.0 / Math.Max(1, source.DpiX)
            : ImageMinWidth;
    }

    private void ConfigureImagesInDocument()
    {
        foreach (var image in FindVisualChildren<System.Windows.Controls.Image>(Editor))
        {
            ConfigureImage(image);
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private enum ImageResizeEdge
    {
        None,
        Left,
        Top,
        Right,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private void ApplyHeadingStyle(string style)
    {
        var paragraph = Editor.CaretPosition.Paragraph;
        if (paragraph is null)
        {
            paragraph = new Paragraph();
            Editor.Document.Blocks.Add(paragraph);
            Editor.CaretPosition = paragraph.ContentStart;
        }

        switch (style)
        {
            case "Title":
                ApplyParagraphStyle(paragraph, ViewModel.FontSize + 10, FontWeights.Bold, 0, 0, 0, 12);
                break;
            case "Heading1":
                ApplyParagraphStyle(paragraph, ViewModel.FontSize + 7, FontWeights.Bold, 0, 0, 0, 10);
                break;
            case "Heading2":
                ApplyParagraphStyle(paragraph, ViewModel.FontSize + 5, FontWeights.SemiBold, 0, 0, 0, 9);
                break;
            case "Heading3":
                ApplyParagraphStyle(paragraph, ViewModel.FontSize + 3, FontWeights.SemiBold, 0, 0, 0, 8);
                break;
            default:
                paragraph.ClearValue(TextElement.FontSizeProperty);
                paragraph.ClearValue(TextElement.FontWeightProperty);
                paragraph.ClearValue(FrameworkContentElement.StyleProperty);
                paragraph.Margin = new Thickness(0, 0, 0, 8);
                paragraph.LineHeight = 22;
                break;
        }
    }

    private static void ApplyParagraphStyle(
        Paragraph paragraph,
        double fontSize,
        FontWeight fontWeight,
        double left,
        double top,
        double right,
        double bottom)
    {
        paragraph.FontSize = Math.Round(fontSize);
        paragraph.FontWeight = fontWeight;
        paragraph.Margin = new Thickness(left, top, right, bottom);
        paragraph.LineHeight = Math.Round(fontSize * 1.35);
    }

    private void InsertHyperlink(string displayText, Uri uri)
    {
        if (!Editor.Selection.IsEmpty)
        {
            Editor.Selection.Text = string.Empty;
        }

        var insertionPosition = Editor.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
        if (insertionPosition?.Paragraph is null)
        {
            var paragraph = new Paragraph();
            Editor.Document.Blocks.Add(paragraph);
            insertionPosition = paragraph.ContentStart;
        }

        var hyperlink = new Hyperlink(new Run(displayText), insertionPosition)
        {
            NavigateUri = uri,
            Foreground = ToBrush(ViewModel.LinkForegroundHex),
            TextDecorations = TextDecorations.Underline
        };

        var trailingSpace = new Run(" ");
        if (hyperlink.Parent is Paragraph paragraphParent)
        {
            paragraphParent.Inlines.InsertAfter(hyperlink, trailingSpace);
            Editor.CaretPosition = trailingSpace.ContentEnd;
        }
        else if (hyperlink.Parent is Span spanParent)
        {
            spanParent.Inlines.InsertAfter(hyperlink, trailingSpace);
            Editor.CaretPosition = trailingSpace.ContentEnd;
        }
    }

    private bool TryInsertTextClipLink(TextClipModel clip)
    {
        try
        {
            var insertionPosition = GetSafeTextClipInsertionPosition(Editor.CaretPosition);
            if (insertionPosition?.Paragraph is null)
            {
                var paragraph = new Paragraph();
                Editor.Document.Blocks.Add(paragraph);
                insertionPosition = paragraph.ContentStart;
            }

            var hyperlink = new Hyperlink(new Run(CreateTextClipLabel(clip)), insertionPosition)
            {
                NavigateUri = CreateTextClipUri(clip.Id)
            };
            ConfigureTextClipLink(hyperlink);

            var trailingSpace = new Run(" ");
            if (hyperlink.Parent is Paragraph paragraphParent)
            {
                paragraphParent.Inlines.InsertAfter(hyperlink, trailingSpace);
                Editor.CaretPosition = trailingSpace.ContentEnd;
            }
            else if (hyperlink.Parent is Span spanParent)
            {
                spanParent.Inlines.InsertAfter(hyperlink, trailingSpace);
                Editor.CaretPosition = trailingSpace.ContentEnd;
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private TextPointer? GetSafeTextClipInsertionPosition(TextPointer position)
    {
        var insertionPosition = position.GetInsertionPosition(LogicalDirection.Forward) ?? position;
        var current = insertionPosition.Parent as DependencyObject;
        while (current is not null)
        {
            if (current is Hyperlink containingHyperlink)
            {
                if (containingHyperlink.NextInline is Run { Text: " " } separator)
                {
                    return separator.ContentEnd.GetInsertionPosition(LogicalDirection.Forward)
                           ?? separator.ContentEnd;
                }

                return containingHyperlink.ElementEnd.GetInsertionPosition(LogicalDirection.Forward)
                       ?? containingHyperlink.ElementEnd;
            }

            current = LogicalTreeHelper.GetParent(current);
        }

        return insertionPosition;
    }

    private void ConfigureTextClipLinksInDocument()
    {
        foreach (var hyperlink in FindLogicalTextElements(Editor.Document).OfType<Hyperlink>())
        {
            if (TryGetTextClipId(hyperlink.NavigateUri, out _))
            {
                ConfigureTextClipLink(hyperlink);
            }
        }
    }

    private void ConfigureTextClipLink(Hyperlink hyperlink)
    {
        hyperlink.Foreground = ToBrush(ViewModel.LinkForegroundHex);
        hyperlink.Background = ToBrush("#72FFFFFF");
        hyperlink.FontSize = 12.5;
        hyperlink.FontWeight = FontWeights.SemiBold;
        hyperlink.TextDecorations = null;
        hyperlink.Cursor = System.Windows.Input.Cursors.Hand;
        hyperlink.Focusable = true;
        hyperlink.ToolTip = LocalizationService.Get("TextClipChipTooltip");
        hyperlink.ContextMenu = CreateTextClipContextMenu(hyperlink);
        hyperlink.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(TextClipLink_PreviewMouseDown));
        hyperlink.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(TextClipLink_PreviewMouseDown), true);
        hyperlink.RemoveHandler(Mouse.MouseUpEvent, new MouseButtonEventHandler(TextClipLink_MouseUp));
        hyperlink.AddHandler(Mouse.MouseUpEvent, new MouseButtonEventHandler(TextClipLink_MouseUp), true);
        hyperlink.RemoveHandler(Mouse.QueryCursorEvent, new QueryCursorEventHandler(TextClipLink_QueryCursor));
        hyperlink.AddHandler(Mouse.QueryCursorEvent, new QueryCursorEventHandler(TextClipLink_QueryCursor), true);
    }

    private ContextMenu CreateTextClipContextMenu(Hyperlink hyperlink)
    {
        var menu = new ContextMenu();

        var preview = new MenuItem { Header = LocalizationService.Get("TextClipContextPreview") };
        preview.Click += (_, _) =>
        {
            if (TryGetTextClipId(hyperlink.NavigateUri, out var clipId))
            {
                ShowTextClipPreview(clipId);
            }
        };
        menu.Items.Add(preview);

        var copy = new MenuItem { Header = LocalizationService.Get("TextClipContextCopy") };
        copy.Click += (_, _) => CopyTextClip(hyperlink);
        menu.Items.Add(copy);

        menu.Items.Add(new Separator());
        var remove = new MenuItem { Header = LocalizationService.Get("TextClipContextRemove") };
        remove.Click += (_, _) => RemoveTextClip(hyperlink);
        menu.Items.Add(remove);

        return menu;
    }

    private void CopyTextClip(Hyperlink hyperlink)
    {
        if (!TryGetTextClipId(hyperlink.NavigateUri, out var clipId)
            || !ViewModel.TryGetTextClip(clipId, out var clip)
            || clip is null)
        {
            ShowTextClipMessage(LocalizationService.Get("TextClipMissing"), "TextClipPreviewTitle");
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(clip.Content);
        }
        catch
        {
            ShowTextClipMessage(LocalizationService.Get("TextClipClipboardError"), "TextClipPreviewTitle");
        }
    }

    private void RemoveTextClip(Hyperlink hyperlink)
    {
        if (!TryGetTextClipId(hyperlink.NavigateUri, out _))
        {
            return;
        }

        _isApplyingBatchEdit = true;
        try
        {
            var trailingInline = hyperlink.NextInline;
            RemoveInlineFromParent(hyperlink);
            if (trailingInline is Run { Text: " " })
            {
                RemoveInlineFromParent(trailingInline);
            }

        }
        finally
        {
            _isApplyingBatchEdit = false;
        }

        SaveEditorContent();
        Editor.Focus();
    }

    private static void RemoveInlineFromParent(Inline inline)
    {
        switch (inline.Parent)
        {
            case Paragraph paragraph:
                paragraph.Inlines.Remove(inline);
                break;
            case Span span:
                span.Inlines.Remove(inline);
                break;
        }
    }

    private void TextClipLink_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || sender is not Hyperlink hyperlink)
        {
            return;
        }

        _pressedTextClipLink = hyperlink;
        _textClipMouseDownPoint = e.GetPosition(Editor);
    }

    private void TextClipLink_MouseUp(object sender, MouseButtonEventArgs e)
    {
        var pressedLink = _pressedTextClipLink;
        _pressedTextClipLink = null;
        if (e.ChangedButton != MouseButton.Left
            || sender is not Hyperlink hyperlink
            || !ReferenceEquals(pressedLink, hyperlink))
        {
            return;
        }

        var position = e.GetPosition(Editor);
        if (Math.Abs(position.X - _textClipMouseDownPoint.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(position.Y - _textClipMouseDownPoint.Y) >= SystemParameters.MinimumVerticalDragDistance
            || !TryGetTextClipId(hyperlink.NavigateUri, out var clipId))
        {
            return;
        }

        e.Handled = true;
        ShowTextClipPreview(clipId);
    }

    private static void TextClipLink_QueryCursor(object sender, QueryCursorEventArgs e)
    {
        e.Cursor = System.Windows.Input.Cursors.Hand;
        e.Handled = true;
    }

    private void ShowTextClipPreview(Guid clipId)
    {
        if (!ViewModel.TryGetTextClip(clipId, out var clip) || clip is null)
        {
            ShowTextClipMessage(LocalizationService.Get("TextClipMissing"), "TextClipPreviewTitle");
            return;
        }

        if (_textClipPreviewWindows.TryGetValue(clipId, out var existingWindow))
        {
            if (existingWindow.WindowState == WindowState.Minimized)
            {
                existingWindow.WindowState = WindowState.Normal;
            }

            existingWindow.Activate();
            return;
        }

        var previewWindow = new TextClipPreviewWindow(clip)
        {
            Owner = this
        };
        previewWindow.Closed += (_, _) => _textClipPreviewWindows.Remove(clipId);
        _textClipPreviewWindows[clipId] = previewWindow;
        previewWindow.Show();
        previewWindow.Activate();
    }

    private void CloseTextClipPreviews()
    {
        foreach (var previewWindow in _textClipPreviewWindows.Values.ToList())
        {
            previewWindow.Close();
        }

        _textClipPreviewWindows.Clear();
    }

    private static Uri CreateTextClipUri(Guid clipId)
    {
        return new Uri($"{TextClipUriScheme}://{TextClipUriHost}/{clipId:D}", UriKind.Absolute);
    }

    private static bool TryGetTextClipId(Uri? uri, out Guid clipId)
    {
        clipId = Guid.Empty;
        return uri is not null
            && string.Equals(uri.Scheme, TextClipUriScheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, TextClipUriHost, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(uri.AbsolutePath.Trim('/'), out clipId);
    }

    private static string CreateTextClipLabel(TextClipModel clip)
    {
        var title = TruncateTextClipTitle(clip.Title.Replace('\r', ' ').Replace('\n', ' ').Trim());

        var extension = clip.FileExtension.TrimStart('.');
        var kind = string.IsNullOrWhiteSpace(extension) ? "TEXT" : extension.ToUpperInvariant();
        return $"  ▤  {title}   {kind}  ";
    }

    private static string TruncateTextClipTitle(string title)
    {
        const int maximumVisualUnits = 26;
        var visualUnits = 0;
        var builder = new StringBuilder();

        foreach (var rune in title.EnumerateRunes())
        {
            var runeUnits = rune.Value <= 0x7F ? 1 : 2;
            if (visualUnits + runeUnits > maximumVisualUnits - 1)
            {
                builder.Append('…');
                return builder.ToString();
            }

            builder.Append(rune.ToString());
            visualUnits += runeUnits;
        }

        return builder.ToString();
    }

    private static string NormalizeUrl(string url)
    {
        var trimmedUrl = url.Trim();
        if (string.IsNullOrWhiteSpace(trimmedUrl)
            || trimmedUrl.Contains("://", StringComparison.Ordinal)
            || trimmedUrl.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmedUrl;
        }

        return $"https://{trimmedUrl}";
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (TryGetTextClipId(e.Uri, out var clipId))
        {
            ShowTextClipPreview(clipId);
            e.Handled = true;
            return;
        }

        if (string.Equals(e.Uri.Scheme, TextClipUriScheme, StringComparison.OrdinalIgnoreCase))
        {
            ShowTextClipMessage(LocalizationService.Get("TextClipMissing"), "TextClipPreviewTitle");
            e.Handled = true;
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore shell failures; the note content itself should stay intact.
        }

        e.Handled = true;
    }

    private void ToggleStrikethrough()
    {
        var selection = Editor.Selection;
        var value = selection.GetPropertyValue(Inline.TextDecorationsProperty);
        var hasStrike = value is TextDecorationCollection decorations
            && decorations.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough);

        selection.ApplyPropertyValue(
            Inline.TextDecorationsProperty,
            hasStrike ? null : TextDecorations.Strikethrough);
    }

    private void ToggleTodoLine()
    {
        var paragraph = Editor.CaretPosition.Paragraph;
        if (paragraph is null)
        {
            paragraph = new Paragraph();
            Editor.Document.Blocks.Add(paragraph);
            Editor.CaretPosition = paragraph.ContentStart;
        }

        var textRange = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
        var text = textRange.Text;

        if (text.StartsWith("☐ ", StringComparison.Ordinal))
        {
            ReplaceParagraphPrefix(paragraph, "☐ ", "☑ ");
        }
        else if (text.StartsWith("☑ ", StringComparison.Ordinal))
        {
            ReplaceParagraphPrefix(paragraph, "☑ ", "☐ ");
        }
        else
        {
            if (paragraph.Inlines.FirstInline is null)
            {
                paragraph.Inlines.Add(new Run("☐ "));
            }
            else
            {
                paragraph.Inlines.InsertBefore(paragraph.Inlines.FirstInline, new Run("☐ "));
            }
        }
    }

    private static void ReplaceParagraphPrefix(Paragraph paragraph, string oldPrefix, string newPrefix)
    {
        var text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;
        paragraph.Inlines.Clear();
        paragraph.Inlines.Add(new Run(newPrefix + text[oldPrefix.Length..].TrimEnd('\r', '\n')));
    }

    private static System.Windows.Media.Brush ToBrush(string hex)
    {
        return (System.Windows.Media.Brush)(new BrushConverter().ConvertFromString(hex) ?? System.Windows.Media.Brushes.Transparent);
    }

    private static System.Windows.Media.Color ToColor(string hex)
    {
        return (System.Windows.Media.Color)(System.Windows.Media.ColorConverter.ConvertFromString(hex) ?? System.Windows.Media.Colors.Transparent);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        ApplyTaskbarVisibility(showInTaskbar: false);
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);
    }

    private void ApplyTaskbarVisibility(bool showInTaskbar)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var extendedStyle = NativeMethods.GetWindowLongPtr(handle, GwlExStyle).ToInt64();

        if (showInTaskbar)
        {
            extendedStyle &= ~WsExToolWindow;
            extendedStyle |= WsExAppWindow;
        }
        else
        {
            extendedStyle |= WsExToolWindow;
            extendedStyle &= ~WsExAppWindow;
        }

        NativeMethods.SetWindowLongPtr(handle, GwlExStyle, new IntPtr(extendedStyle));
        NativeMethods.SetWindowPos(
            handle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && ShouldBlockMinimize())
        {
            QueueShowDesktopRecovery();
            return;
        }

        if (_isTaskbarMinimized && WindowState != WindowState.Minimized)
        {
            _isTaskbarMinimized = false;
            ShowInTaskbar = false;
            ApplyTaskbarVisibility(showInTaskbar: false);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg is WmDisplayChange or WmDpiChanged)
        {
            _windowResizeThumb?.CancelDrag();
            Dispatcher.BeginInvoke(FitWindowToCurrentWorkArea, DispatcherPriority.Loaded);
        }

        if (!ShouldBlockMinimize())
        {
            return IntPtr.Zero;
        }

        if (msg == WmSysCommand && (wParam.ToInt64() & 0xFFF0) == ScMinimize)
        {
            handled = true;
            QueueShowDesktopRecovery();
            return IntPtr.Zero;
        }

        if (msg == WmSize && wParam.ToInt32() == SizeMinimized)
        {
            handled = true;
            QueueShowDesktopRecovery();
            return IntPtr.Zero;
        }

        if (msg == WmShowWindow && wParam == IntPtr.Zero)
        {
            handled = true;
            QueueShowDesktopRecovery();
            return IntPtr.Zero;
        }

        if (msg == WmWindowPosChanging && lParam != IntPtr.Zero)
        {
            var windowPos = Marshal.PtrToStructure<WindowPos>(lParam);
            if ((windowPos.Flags & SwpHideWindow) != 0)
            {
                windowPos.Flags &= ~SwpHideWindow;
                windowPos.Flags |= SwpShowWindow | SwpNoActivate;
                Marshal.StructureToPtr(windowPos, lParam, fDeleteOld: false);

                handled = true;
                QueueShowDesktopRecovery();
                return IntPtr.Zero;
            }
        }

        return IntPtr.Zero;
    }

    private bool CanResizeFromWindowEdges()
    {
        return !_isIconCollapsed
            && WindowState == WindowState.Normal
            && ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
    }

    private bool ShouldBlockMinimize()
    {
        return ViewModel.Visible
            && !_allowClose
            && !_isTaskbarMinimized
            && App.Current.KeepNotesVisibleAfterShowDesktop;
    }

    private void QueueShowDesktopRecovery()
    {
        if (!ShouldBlockMinimize())
        {
            return;
        }

        _showDesktopRecoveryAttempts = Math.Max(_showDesktopRecoveryAttempts, 30);
        Dispatcher.BeginInvoke(RestoreAfterShowDesktop);
    }

    private void ShowDesktopRecoveryTimer_Tick(object? sender, EventArgs e)
    {
        if (!ShouldBlockMinimize())
        {
            _showDesktopRecoveryAttempts = 0;
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (_showDesktopRecoveryAttempts > 0
            || IsNativeWindowSuppressed(handle)
            || IsShellDesktopForeground())
        {
            RestoreAfterShowDesktop();
        }
    }

    private void RestoreAfterShowDesktop()
    {
        if (!ShouldBlockMinimize())
        {
            _showDesktopRecoveryAttempts = 0;
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;

        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        if (handle != IntPtr.Zero)
        {
            NativeMethods.ShowWindowAsync(handle, SwShownoactivate);
            NativeMethods.SetWindowPos(
                handle,
                ViewModel.Topmost ? HwndTopmost : HwndTop,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow | SwpFrameChanged);
        }

        ShowInTaskbar = false;
        ApplyTaskbarVisibility(showInTaskbar: false);

        if (_showDesktopRecoveryAttempts > 0)
        {
            _showDesktopRecoveryAttempts--;
        }
    }

    private static bool IsNativeWindowSuppressed(IntPtr handle)
    {
        return handle != IntPtr.Zero
            && (NativeMethods.IsIconic(handle)
                || !NativeMethods.IsWindowVisible(handle)
                || NativeMethods.IsWindowCloaked(handle));
    }

    private static bool IsShellDesktopForeground()
    {
        var foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        var className = NativeMethods.GetWindowClassName(foregroundWindow);
        return string.Equals(className, "Progman", StringComparison.Ordinal)
            || string.Equals(className, "WorkerW", StringComparison.Ordinal)
            || string.Equals(className, "SHELLDLL_DefView", StringComparison.Ordinal);
    }

    private void UpdateGeometry(Rect? requestedBounds = null)
    {
        if (!IsLoaded || _isIconCollapsed || _isUpdatingWindowBounds || WindowState != WindowState.Normal)
        {
            return;
        }

        var bounds = requestedBounds ?? new Rect(Left, Top, ActualWidth, ActualHeight);
        if (double.IsFinite(bounds.Left))
        {
            ViewModel.X = bounds.Left;
        }

        if (double.IsFinite(bounds.Top))
        {
            ViewModel.Y = bounds.Top;
        }

        if (double.IsFinite(bounds.Width) && bounds.Width > 0)
        {
            ViewModel.Width = bounds.Width;
        }

        if (double.IsFinite(bounds.Height) && bounds.Height > 0)
        {
            ViewModel.Height = bounds.Height;
        }
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Button)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPos
    {
        public IntPtr Hwnd;
        public IntPtr HwndInsertAfter;
        public int X;
        public int Y;
        public int Cx;
        public int Cy;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }
    }

    private static class NativeMethods
    {
        private const int DwmwaCloaked = 14;

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        public static bool IsWindowCloaked(IntPtr hWnd)
        {
            try
            {
                return DwmGetWindowAttribute(hWnd, DwmwaCloaked, out var cloaked, Marshal.SizeOf<int>()) == 0
                    && cloaked != 0;
            }
            catch
            {
                return false;
            }
        }

        public static string GetWindowClassName(IntPtr hWnd)
        {
            var className = new StringBuilder(256);
            return GetClassName(hWnd, className, className.Capacity) > 0
                ? className.ToString()
                : string.Empty;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        internal static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern bool GetCursorPos(out NativePoint lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            out int pvAttribute,
            int cbAttribute);
    }
}
