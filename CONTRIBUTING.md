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
dotnet run --project src/DeskStickyNotes/DeskStickyNotes.csproj
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
- `dotnet run --project tests/DeskStickyNotes.RegressionTests -c Release` passes on Windows.
- New UI text is translated in all seven `src/DeskStickyNotes/Localization/Strings.*.xaml` dictionaries: English, Simplified/Traditional Chinese, French, Spanish, Portuguese, and Japanese. Keep resource keys and format placeholders identical.
- Shortcut changes preserve working registrations on conflict and restore hidden, minimized, and collapsed notes without changing saved pin preferences.
- Update the English/Chinese README and `docs/开发提示词.md` when product behavior changes. Refresh affected `docs/images/` previews with sample data and check that descriptions match the shipped version.
- The app still works without network access.
- Note content and settings remain local.
- Large binaries are not committed. Build outputs belong in `bin/`, `obj/`, or `artifacts/`.

The regression executable briefly opens overlapping sample windows for native stacking, focus, and minimize/restore checks. Its WPF host skips application startup and user storage. To render reproducible interface previews with sample content:

```powershell
dotnet run --project tests/DeskStickyNotes.RegressionTests -c Release -- --render-preview artifacts/qa
```

This also renders settings, the tray menu, and an overview with light/dark tray-icon checks. After editing the native vector icon master, regenerate the shipped icon assets:

```powershell
./scripts/build-icons.ps1
```

Before releasing shell-behavior changes, manually check two overlapping pinned notes and a mixed pinned/unpinned pair with Win+D, clicking the desktop, switching between notes, intentional taskbar minimization, and the keep-after-Win+D setting disabled. Native regression checks do not reproduce Explorer's complete Show Desktop behavior.

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
