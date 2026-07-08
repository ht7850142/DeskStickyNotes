# Contributing

Thanks for helping improve DeskStickyNotes.

This project is intentionally small. The best contributions keep it focused on lightweight, local-first, Win7-style sticky notes for Windows 10/11.

## Development Setup

Requirements:

- Windows 10/11
- .NET 8 Desktop SDK
- Inno Setup 6, only for installer packaging

Run from source:

```powershell
dotnet run
```

Build:

```powershell
dotnet build -c Release
```

Create release packages:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1 -AllInstallers
```

## Before Opening A Pull Request

Please check:

- `dotnet build -c Release` passes.
- New UI text is added to both `src/DeskStickyNotes/Localization/Strings.en-US.xaml` and `src/DeskStickyNotes/Localization/Strings.zh-Hans.xaml`.
- The app still works without network access.
- Note content and settings remain local.
- Large binaries are not committed. Build outputs belong in `bin/`, `obj/`, or `artifacts/`.

## Code Style

- Prefer existing MVVM patterns already used in the repository.
- Keep features small and native to WPF/.NET.
- Avoid large third-party UI frameworks.
- Use `System.Text.Json` and existing services unless there is a clear reason not to.
- Keep Windows shell behavior explicit and documented, especially for taskbar, tray, topmost, and Win+D behavior.

## Good First Issues

Useful small contributions include:

- UI polish that preserves the lightweight feel
- More localization resources
- Accessibility improvements
- Installer wording improvements
- Better documentation and screenshots
- Focused bug fixes around note persistence, multiple monitors, or tray behavior

## Reporting Bugs

Please include:

- DeskStickyNotes version
- Windows version
- Install type: installer, portable, framework-dependent, or runtime/self-contained
- Steps to reproduce
- Expected behavior
- Actual behavior
- Whether the note was topmost, hidden, collapsed, or restored from startup

Do not paste private note content unless it is necessary. Redact personal data from `notes.json` or screenshots.
