# Changelog

All notable changes to DeskStickyNotes are documented here.

The project follows practical semantic versioning while it is pre-1.0. Breaking behavior may still change between minor versions.

## [Unreleased]

- Add screenshots to the README.
- Add automated UI smoke tests if the project grows beyond manual verification.

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
