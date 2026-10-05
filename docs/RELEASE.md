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

Run the window geometry and persistence regression checks before packaging:

```powershell
dotnet run --project tests/DeskStickyNotes.RegressionTests -c Release
```

These checks use temporary storage and do not read or write real notes.

## 3. Prepare Runtime Installer

The runtime-bundled installer expects this file at the repository root:

```text
windowsdesktop-runtime-8.0.31-win-x64.exe
```

The packaging script downloads a missing installer from Microsoft's release metadata and verifies its SHA-512 hash. Do not commit it. It is ignored by `.gitignore`.

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
artifacts\installer\DeskStickyNotes-<version>-SHA256SUMS.txt
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
- all eight resize handles work at compact and expanded sizes
- empty notes keep manually selected sizes after restarting
- icons at each screen edge expand inside the work area and stay anchored when collapsed again
- compact toolbar wraps without losing editing tools
- moving between monitors and changing DPI or resolution keeps notes reachable
- note names save independently, update captions/icon tooltips, and survive restarts
- tray note list refreshes names and permits independent Show/Hide actions, with hidden state saved across restarts
- imported text slices still preview after their source files are deleted

## 6. Publish

Create a GitHub Release with:

- short summary
- known limitations
- all four release artifacts
- SHA-256 checksum manifest
- changelog section for the version

Mention that `-fd` packages require the .NET 8 Desktop Runtime and `runtime` packages do not.

Commit and push the source first, then create a draft release targeting that exact commit. Upload and verify all five assets before publishing. Large binaries belong in release assets rather than Git history.
