# Changelog

All notable changes to DeskStickyNotes are documented here.

The project follows practical semantic versioning while it is pre-1.0. Breaking behavior may still change between minor versions.

## [Unreleased]

## [0.4.0] - 2026-10-05

- Fixed note resizing with drag handles on all four edges and corners, including transparent borderless windows.
- Preserve manually resized notes, including empty notes, when saving and restarting.
- Keep notes inside the current monitor's work area when expanding a collapsed icon, including high-DPI displays.
- Added compact text slices that preserve full pasted text behind a clickable preview.
- Added drag-and-drop, clipboard-file paste, and file-picker import for common text, Markdown, data, log, and source-code files.
- Text files are imported as local snapshots with encoding, binary-content, per-file, and per-note size safeguards.
- Added independent note names with title-bar/icon context menus and an F2 shortcut; names persist across restarts.
- Added a tray note list with live names, visible/hidden indicators, and per-note Show/Hide actions.
- Added compact 300 × 180 minimum sizing with a wrapping toolbar that keeps editing tools accessible.
- Keep edge icons anchored through repeated expansion and collapse.
- Fit restored notes to the work area at startup and after display or DPI changes.
- Added Enter/Space restoration for collapsed notes and Escape dismissal for editor flyouts.
- Removed forced synchronous layout on every resize movement.
- Added Windows regression checks for sizing, names, persistence, compact layouts, text encodings, and snapshots.
- Updated runtime packages to .NET Desktop Runtime 8.0.31 with verified downloads and SHA-256 release checksums.

## [0.3.0] - 2026-07-20

- Added per-note background opacity from 30% to 100% without fading text, controls, or images.
- Added Auto, Dark, and Light note text color modes with contrast-aware link colors.
- Preserved the existing opaque appearance for notes created by earlier versions.

## [0.2.9] - 2026-07-16

- Fixed maximized notes so dragging the title bar restores the previous window size.
- Preserved normal window bounds when collapsing a maximized note into an icon.

## [0.2.8] - 2026-07-10

- Automatically fit pasted screenshots to the available note width.
- Added edge and corner drag resizing, Ctrl + mouse wheel resizing, and a right-click size menu for images.
- Persist embedded images with rich note content while retaining compatibility with existing text notes.

## [0.2.7] - 2026-07-08

- Fixed hyperlink dialog spacing so the URL field is not clipped by the action buttons.
- Added a clear disabled state for primary buttons.

## [0.2.6] - 2026-07-08

- Made hyperlink insertion clearer by requiring both display text and link address.
- Enlarged the hyperlink dialog and added explanatory copy in English and Simplified Chinese.

## [0.2.5] - 2026-07-08

- Changed tray icon double-click to show existing notes instead of creating a new note.
- Updated the topmost button to use one pin icon with normal and inverted states.
- Hidden the editor scrollbar during normal note editing.

## [0.2.4] - 2026-07-08

- Removed debug symbol files from public release packages.
- Disabled Release build debug symbols to reduce local path exposure in distributed artifacts.

## [0.2.3] - 2026-07-08

- Added a runtime guard that prevents empty notes from saving oversized heights.
- Applied the initial note size explicitly when creating note windows.

## [0.2.2] - 2026-07-08

- Reduced the default new-note height to the compact minimum.
- Compact oversized empty restored notes so first launch after upgrade does not show a huge blank note.

## [0.2.1] - 2026-07-08

- Made newly created notes smaller by default.
- Added a title-bar `+` button for creating a new note from any note window.

## [0.2] - 2026-07-08

- Prepared the project for an open-source release.
- Added English and Simplified Chinese README files, contribution guide, security policy, release guide, architecture notes, changelog, license, and GitHub issue/PR templates.
- Hardened `.gitignore` for build outputs, local runtime installers, secrets, certificates, and publish credentials.

## [0.1.12] - 2026-07-08

- Replaced system-style editor flyouts with custom note-themed popup menus.
- Unified heading and color picker flyout styling.

## [0.1.11] - 2026-07-08

- Improved collapsed-note icon mode so it renders without the sticky-note background panel.

## [0.1.10] - 2026-07-08

- Made collapsed-note icons draggable.
- Preserved the expanded note size while moving the collapsed icon.

## [0.1.9] - 2026-07-08

- Added collapse-to-icon behavior for notes.
- Improved topmost behavior for compact desktop workflows.

## [0.1.0] - 2026-07-08

- Initial public-ready app foundation.
- Added tray-managed sticky notes, local storage, settings, localization, rich text editing, startup support, installers, and portable packages.
