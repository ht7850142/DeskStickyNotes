#ifndef SourceDir
#define SourceDir "..\artifacts\publish\win-x64"
#endif

#ifndef OutputDir
#define OutputDir "..\artifacts\installer"
#endif

#ifndef IncludeRuntime
#define IncludeRuntime 0
#endif

#ifndef RuntimeInstaller
#define RuntimeInstaller "..\windowsdesktop-runtime-8.0.28-win-x64.exe"
#endif

#ifndef PackageSuffix
#define PackageSuffix ""
#endif

#define RuntimeInstallerName "windowsdesktop-runtime-8.0.28-win-x64.exe"
#define MyAppName "DeskStickyNotes"
#define MyAppVersion "0.2.8"
#define MyAppPublisher "DeskStickyNotes"
#define MyAppExeName "DeskStickyNotes.exe"

[Setup]
AppId={{9D24621B-6C35-47C9-B08F-B598C7AFC54D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableDirPage=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
UsePreviousAppDir=yes
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no
OutputDir={#OutputDir}
OutputBaseFilename=DeskStickyNotesSetup-{#MyAppVersion}{#PackageSuffix}
SetupIconFile=..\src\DeskStickyNotes\Assets\AppIcon.ico
WizardSmallImageFile=WizardSmallImage.bmp
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
#if IncludeRuntime
Source: "{#RuntimeInstaller}"; DestDir: "{tmp}"; DestName: "{#RuntimeInstallerName}"; Flags: deleteafterinstall
#endif

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
#if IncludeRuntime
Filename: "{tmp}\{#RuntimeInstallerName}"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Microsoft .NET 8 Desktop Runtime..."; Check: not IsWindowsDesktopRuntime8Installed; Flags: waituntilterminated
#endif
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsWindowsDesktopRuntime8Installed: Boolean;
begin
  Result :=
    DirExists(ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App\8.0.28')) or
    DirExists(ExpandConstant('{commonpf32}\dotnet\shared\Microsoft.WindowsDesktop.App\8.0.28'));
end;
