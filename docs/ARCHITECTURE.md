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
- registers configurable global shortcuts and releases them on exit
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
  HotkeyGesture.cs
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
- uses direct checked-item toggles for individual visibility, with full titles retained in accessible names and tooltips
- renders a compact, illustrated action menu using native ToolStrip keyboard/navigation behavior; high-contrast mode uses the system renderer
- calls back into app-level actions

`StartupService`

- writes or removes the `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value
- does not require administrator privileges

`LocalizationService`

- switches between complete Simplified Chinese, Traditional Chinese, English, French, Spanish, Portuguese, and Japanese resource dictionaries
- resolves Auto from the original Windows UI culture, including regional Chinese and Western-language variants
- updates UI font families and text language together with resource dictionaries

`GlobalHotkeyService` and `HotkeyRegistrationManager`

- receive `WM_HOTKEY` through a message-only WPF HWND, using `RegisterHotKey` with `MOD_NOREPEAT`
- reserve replacement combinations before releasing existing bindings; duplicates and native conflicts leave the working configuration intact
- reuse existing registration IDs when swapping the two actions, and support independent disabling through empty settings values
- suspend registrations while shortcut fields record input, then resume when focus leaves the field
- create notes or cycle through saved notes in creation order, restoring hidden, minimized, and collapsed targets
- temporarily reveal an unpinned target above a pinned peer until deactivation without changing its saved pin preference

`DesktopNoteRecoveryService`

- owns a single application-level 250 ms background timer, started only while note windows are registered
- coalesces shell hide/minimize notifications and restores the group only on desktop entry or actual suppression
- maintains activation order and restores bottom to top without activating the windows, preserving Windows' normal/topmost bands
- caps suppression recovery at three attempts; idle desktop ticks never write z-order
- excludes intentionally hidden/taskbar-minimized notes and notes with desktop recovery disabled

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

Collapsed icon mode keeps the icon topmost without mutating the model's saved pin preference, reduces the window to a small draggable icon, and preserves the previous expanded size. Expansion restores the saved pin preference and fits the window to the icon's current monitor work area using device-to-WPF coordinate conversion, keeping it clear of screen edges and the taskbar. A separate icon anchor prevents repeated collapse/restore from moving edge icons.

Transparent borderless notes use WPF `Thumb` controls over the visible edges and corners for resizing. Drag distances use screen coordinates and the window's DPI scale so top/left resizing stays stable. Geometry updates are batched before saving, and persistence preserves user-selected sizes for empty notes as well as populated ones.

## Persistence

The toolbar wraps whole groups at compact widths. Shared note-control colors, component states, focus visuals, and flyout styles live in `Themes/Controls.xaml`; the five paper palettes remain in `NotePalette`. Formatting indicators follow the editor selection; empty-note guidance disappears for both text and embedded content. Scrollbars appear when long notes overflow. The title bar supports dragging and double-click renaming; collapsing uses its own button to avoid accidental folding when switching between overlapping notes.

Notes fit to the current work area on startup and display/DPI changes, and resizing lets WPF schedule layout instead of forcing layout on every mouse movement. Per-note names are normalized in the model, edited through an independent dialog, and reflected in captions and icon tooltips.

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
- new-note global shortcut (default `Ctrl+Alt+N`)
- next-note global shortcut (default `Ctrl+Alt+PageDown`)

Settings schema 4 adds the shortcut fields. Missing or null values receive defaults; explicit empty values remain disabled. Loading preserves existing note content and earlier settings migrations.

## Windows Shell Behavior

DeskStickyNotes uses normal desktop windows. Some shell behavior cannot be perfectly controlled by a regular app:

- `ShowInTaskbar=false` keeps note windows out of the taskbar.
- Topmost mode is reliable for keeping notes above regular windows.
- Win+D "Show Desktop" is shell-level behavior and is only best effort.
- Collapsed icon mode is the recommended compact mode when notes should stay visible but occupy less space.

Recovery does not continuously enforce a note's position within its z-order band. A recovery pass raises the eligible group in its remembered order and then stops. Shell notifications emitted by a pass are ignored while it runs, and repeated suppression is bounded, so two topmost notes cannot perpetually promote themselves above one another. Native regression checks use overlapping sample HWNDs to verify stable stacking, no idle z-order writes, suppression recovery without focus theft, and intentional taskbar minimization. Actual Win+D/Explorer behavior still requires a manual Windows session check.

## Settings and Icon Assets

Settings edit a view-model draft until Save. Color selection updates a sample note, and the font-size slider updates the sample text. The body scrolls independently of the Save/Cancel footer, and the window fits the current work area. Color and default pinning affect new notes; font size retains the existing application-wide behavior. Escape cancels.

Shortcut fields record combinations directly, provide clear/reset controls, and retain Tab navigation. Save validates and registers the complete pair before committing the draft. A localized error stays above the fixed footer when a combination is invalid, duplicated, or occupied; the settings window remains open for correction. All seven locales have key/placeholder contract checks and compiled WPF layout coverage.

`Assets/Icons.xaml` is the editable WPF vector master for the application mark and a simplified small tray mark. `scripts/build-icons.ps1` exports the PNG and multi-resolution ICO files without external rendering packages. Tray frames cover 16–64 pixels; the application icon additionally covers 128 and 256 pixels. The collapsed reminder and settings header use the vector directly.

## Packaging

`scripts/package.ps1` creates:

- framework-dependent portable zip
- self-contained portable zip
- framework-dependent Inno Setup installer
- installer bundled with the .NET Desktop Runtime

`installer/DeskStickyNotes.iss` defines installer behavior, icon, install path, app closing behavior, and runtime bundling.
