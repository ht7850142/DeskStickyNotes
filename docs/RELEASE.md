# Release Guide

This checklist is for maintainers preparing a public release.

## 1. Update Version

Update the version in:

```text
src/DeskStickyNotes/DeskStickyNotes.csproj
scripts/package.ps1
installer/DeskStickyNotes.iss
README.md
README.zh-CN.md
CHANGELOG.md
```

Use the same version everywhere.

## 2. Verify Build

```powershell
dotnet build -c Release
```

If `dotnet` is not on `Path`, run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build -c Release
```

## 3. Prepare Runtime Installer

The runtime-bundled installer expects this file at the repository root:

```text
windowsdesktop-runtime-8.0.28-win-x64.exe
```

Do not commit it. It is ignored by `.gitignore`.

## 4. Build Packages

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1 -AllInstallers
```

If package restore has already completed and the SDK cannot reach its configured package source, reuse the existing assets with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1 -AllInstallers -NoRestore
```

Expected outputs:

```text
artifacts\installer\DeskStickyNotes-portable-<version>-win-x64-fd.zip
artifacts\installer\DeskStickyNotes-portable-<version>-win-x64-runtime.zip
artifacts\installer\DeskStickyNotesSetup-<version>-fd.exe
artifacts\installer\DeskStickyNotesSetup-<version>-runtime.exe
```

Public packages should not contain `.pdb` debug symbol files.

## 5. Smoke Test

Manually verify:

- app starts
- tray icon appears
- new note works
- note content auto-saves
- restart restores notes
- settings window opens
- language switching works
- startup toggle writes/removes the Run registry value
- installer can install over the previous version
- portable package starts from an extracted folder

## 6. Publish

Create a GitHub Release with:

- short summary
- known limitations
- all four release artifacts
- changelog section for the version

Mention that `-fd` packages require the .NET 8 Desktop Runtime and `runtime` packages do not.
