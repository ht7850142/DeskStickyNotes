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
#define RuntimeInstaller "..\windowsdesktop-runtime-8.0.31-win-x64.exe"
#endif

#ifndef PackageSuffix
#define PackageSuffix ""
#endif

#ifndef RuntimeVersion
#define RuntimeVersion "8.0.31"
#endif
#define RuntimeInstallerName "windowsdesktop-runtime-" + RuntimeVersion + "-win-x64.exe"
#define MyAppName "DeskStickyNotes"
#ifndef MyAppVersion
#define MyAppVersion "0.4.0"
#endif
#define MyAppPublisher "DeskStickyNotes"
#define MyAppExeName "DeskStickyNotes.exe"

[Setup]
AppId={{9D24621B-6C35-47C9-B08F-B598C7AFC54D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
VersionInfoVersion={#MyAppVersion}
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
function HasCompatibleDesktopRuntime(const RuntimeDirectory: String): Boolean;
var
  FindRec: TFindRec;
  InstalledVersion, RequiredVersion: Int64;
begin
  Result := False;
  if not StrToVersion('{#RuntimeVersion}', RequiredVersion) then
    Exit;

  if FindFirst(AddBackslash(RuntimeDirectory) + '8.0.*', FindRec) then begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) and
           StrToVersion(FindRec.Name, InstalledVersion) then begin
          if ComparePackedVersion(InstalledVersion, RequiredVersion) >= 0 then begin
            Result := True;
            Break;
          end;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function IsWindowsDesktopRuntime8Installed: Boolean;
var
  InstallLocation: String;
begin
  Result := False;
  if RegQueryStringValue(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64',
    'InstallLocation', InstallLocation) then
    Result := HasCompatibleDesktopRuntime(AddBackslash(InstallLocation) + 'shared\Microsoft.WindowsDesktop.App');

  if not Result then
    Result := HasCompatibleDesktopRuntime(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App'));
end;
