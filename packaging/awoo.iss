; Inno Setup script for awoo, the tray volume control.
;
; awoo is an unpackaged WinUI 3 app and carries both its runtimes, so this
; installer is a plain per-user file copy: no admin rights, no prerequisites
; and nothing machine-wide. Compile it over a self-contained publish:
;
;   dotnet publish src\Awoo\Awoo.csproj -c Release -p:SelfContained=true
;   iscc packaging\awoo.iss /DAppVersion=0.1.0 ^
;        /DPayloadDir=<the publish directory> /DOutputDir=<where to put setup>
;
; Defaults for all three let it run with no /D at all, from the repository root.

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

; Windows' version resource takes four numbers and nothing else, so a
; prerelease version keeps its suffix in AppVersion -- which is what the
; wizard and the filename show -- and drops it here. Pass this whenever
; AppVersion carries a suffix; a plain x.y.z needs no second define.
#ifndef AppVersionNumeric
  #define AppVersionNumeric AppVersion
#endif

#ifndef PayloadDir
  #define PayloadDir "..\src\Awoo\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish"
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

; Two spellings of the name, and the difference matters. AppName is the one
; Windows keeps things under -- the install folder, the Run value and the
; settings folder -- and it stays lowercase, because an upgrade and an
; uninstall find those by name. AppDisplayName is what a person reads.
#define AppName "awoo"
#define AppDisplayName "Awoo"
#define AppExe "awoo.exe"
#define AppPublisher "Awoo contributors"
#define AppUrl "https://github.com/infifox/awoo"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"

[Setup]
; Never change AppId: it is what Windows matches an upgrade and an uninstall by.
AppId={{748E8857-2F48-41CA-9A3B-02FEAA632965}
AppName={#AppDisplayName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersionNumeric}

; Per-user throughout: lowest privileges puts {autopf} under
; %LOCALAPPDATA%\Programs and {autoprograms} in the user's own Start Menu, so
; nothing here ever asks for elevation.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppDisplayName}
DisableProgramGroupPage=yes
DisableDirPage=auto

; Windows 11 only, x64 only -- which is what awoo is built for.
MinVersion=10.0.22000
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

OutputDir={#OutputDir}
OutputBaseFilename=awoo-{#AppVersion}-setup
SetupIconFile=..\src\Awoo\Assets\awoo.ico
LicenseFile=..\LICENSE
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppDisplayName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes

; The payload is large and the app runs from the tray, so ask Windows which
; files are in use rather than leaving a half-replaced install behind.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; NTFS keeps a file's existing spelling when something writes over it, so the
; shortcut an earlier install left called "awoo" would go on reading that way
; in the Start Menu. Taking it away first has the new one created under its
; own name.
Type: files; Name: "{autoprograms}\{#AppName}.lnk"

[Icons]
Name: "{autoprograms}\{#AppDisplayName}"; Filename: "{app}\{#AppExe}"

[Registry]
; "Start with Windows", on by default the way task 06 settled it. awoo writes
; this value itself on a first run and keeps it pointing at wherever it is
; installed, but doing it here means autostart is registered by installing
; rather than by launching. uninsdeletevalue is what takes it away again.
;
; Fresh installs only: the default gets one go, like awoo's own. Turning the
; setting off deletes this value, and an upgrade that wrote it again would
; turn autostart back on behind the person who turned it off.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Check: IsFreshInstall

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppDisplayName}}"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
; awoo's two settings, so an uninstall leaves nothing of it behind.
Type: filesandordirs; Name: "{localappdata}\{#AppName}"

[Code]
var
  FreshInstall: Boolean;

{ Whether this run is a first install rather than an upgrade, read before
  anything is written: by the end of an install the uninstall key exists
  either way. The key is HKCU because the install is per-user. }
function InitializeSetup(): Boolean;
begin
  { ExpandConstant collapses the AppId's doubled brace. }
  FreshInstall := not RegKeyExists(HKCU, ExpandConstant(
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1'));
  Result := True;
end;

function IsFreshInstall(): Boolean;
begin
  Result := FreshInstall;
end;

{ awoo has no main window for Windows to close, so Restart Manager cannot
  always talk it into quitting. Ending it outright costs nothing: every
  setting is written to disk as it is changed. }
procedure StopAwoo;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '',
       SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopAwoo;
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopAwoo;
  Result := True;
end;
