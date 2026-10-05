# Architecture

DeskStickyNotes is a small WPF/.NET 8 desktop app. It uses MVVM-style view models, lightweight services, and local JSON persistence.

The repository uses a conventional .NET layout:

```text
DeskStickyNotes.sln
src/DeskStickyNotes/DeskStickyNotes.csproj
```

## Goals

- Native Windows desktop behavior
- Tray-managed notes
- No account, sync, ads, or telemetry
- Small dependency surface
- Simple packaging for installer and portable releases

## App Lifecycle

`App.xaml.cs` owns the application lifecycle:

- loads settings and notes
- initializes localization
- creates the tray icon
- restores saved note windows
- creates a default note when no notes exist
- saves data before exit

The application uses `ShutdownMode="OnExplicitShutdown"` so closing a note window does not exit the whole app.

## MVVM Shape

```text
src/DeskStickyNotes/
  App.xaml
  App.xaml.cs

src/DeskStickyNotes/Models/
  AppSettings.cs
  LanguageOption.cs
  NoteModel.cs
  NotePalette.cs
  TextClipModel.cs

src/DeskStickyNotes/ViewModels/
  NoteViewModel.cs
  SettingsViewModel.cs
  ObservableObject.cs
  RelayCommand.cs

src/DeskStickyNotes/Views/
  NoteWindow.xaml
  SettingsWindow.xaml
  HyperlinkWindow.xaml
  TextClipPreviewWindow.xaml
```

The view models expose note and settings state. Views handle WPF-specific UI behavior such as rich text editing, window dragging, tray-window interactions, and popup placement.

## Services

`NoteStorageService`

- reads and writes `notes.json` and `settings.json`
- stores data in `%AppData%\DeskStickyNotes`
- uses `System.Text.Json`

`TextFileImportService`

- validates supported text-like files and the 2 MB per-file limit
- decodes UTF and common Windows/Chinese text encodings while rejecting binary content
- creates snapshot-backed `TextClipModel` values

`TrayService`

- wraps `System.Windows.Forms.NotifyIcon`
- owns the tray menu
- refreshes a per-note submenu with current names, visible/hidden state, and independent Show/Hide actions
- calls back into app-level actions

`StartupService`

- writes or removes the `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value
- does not require administrator privileges

`LocalizationService`

- switches between Auto, Simplified Chinese, and English resource dictionaries

## Note Windows

`NoteWindow` is a borderless WPF window:

- `ShowInTaskbar=false` for normal note windows
- custom title bar and toolbar
- rich text content saved as XAML plus plain text fallback
- per-note color palette
- per-note topmost state
- optional collapsed icon mode
- compact text-slice links backed by locally persisted full-text snapshots
- text-file drag and drop with a separate read-only preview window

Collapsed icon mode keeps the note topmost, reduces the window to a small draggable icon, and preserves the previous expanded size. Expansion fits the window to the icon's current monitor work area using device-to-WPF coordinate conversion, keeping it clear of screen edges and the taskbar. A separate icon anchor prevents repeated collapse/restore from moving edge icons.

Transparent borderless notes use WPF `Thumb` controls over the visible edges and corners for resizing. Drag distances use screen coordinates and the window's DPI scale so top/left resizing stays stable. Geometry updates are batched before saving, and persistence preserves user-selected sizes for empty notes as well as populated ones.

## Persistence

The toolbar wraps at compact widths. Notes fit to the current work area on startup and display/DPI changes, and resizing lets WPF schedule layout instead of forcing layout on every mouse movement. Per-note names are normalized in the model, edited through an independent dialog, and reflected in captions and icon tooltips.

Notes are stored as local JSON. Important fields include:

- `id`
- `title` (blank for the localized default name; compatible with older notes)
- `textContent`
- `richContent`
- `textClips` (snapshot content and source metadata)
- `x`
- `y`
- `width`
- `height`
- `color`
- `topmost`
- `visible`
- `updatedAt`

Settings include:

- language
- default color
- default font size
- start with Windows
- default topmost
- keep after Win+D
- auto-save interval

## Windows Shell Behavior

DeskStickyNotes uses normal desktop windows. Some shell behavior cannot be perfectly controlled by a regular app:

- `ShowInTaskbar=false` keeps note windows out of the taskbar.
- Topmost mode is reliable for keeping notes above regular windows.
- Win+D "Show Desktop" is shell-level behavior and is only best effort.
- Collapsed icon mode is the recommended compact mode when notes should stay visible but occupy less space.

## Packaging

`scripts/package.ps1` creates:

- framework-dependent portable zip
- self-contained portable zip
- framework-dependent Inno Setup installer
- installer bundled with the .NET Desktop Runtime

`installer/DeskStickyNotes.iss` defines installer behavior, icon, install path, app closing behavior, and runtime bundling.
