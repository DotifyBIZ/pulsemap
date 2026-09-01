; Pulsemap installer script (Inno Setup) — see docs/adr/0002-installer-innosetup-over-msix.md
; for why InnoSetup was chosen over MSIX. Wraps the self-contained `dotnet publish` output for
; win-x64 as-is (no MSIX identity, no single-file — see that ADR's Implementation Notes).
;
; Local build:
;   dotnet publish src\Pulsemap.App\Pulsemap.App.csproj -c Release -p:PublishProfile=win-x64
;   "C:\Program Files\Inno Setup 7\ISCC.exe" Pulsemap.iss
;
; CI passes the real version: ISCC.exe /DMyAppVersion=1.2.3 Pulsemap.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif

#define MyAppName "Pulsemap"
#define MyAppPublisher "Dotify"
#define MyAppURL "https://github.com/DotifyBIZ/pulsemap"
#define MyAppExeName "Pulsemap.App.exe"
#define MyPublishDir "src\Pulsemap.App\bin\Release\net9.0-windows10.0.19041.0\win-x64\publish"

[Setup]
; Fixed, never regenerate — Inno Setup uses this to recognize upgrades of the same app.
AppId={{67DE5C8C-7FC6-4A87-A320-9E3DF1D1043C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
; Per-user install, no admin rights required — Pulsemap runs on client-site technician
; machines whose Group Policy/admin rights Dotify doesn't control (same reasoning as the
; ADR's InnoSetup-over-MSIX choice). Change to {autopf} + PrivilegesRequired=admin if a
; machine-wide install is ever wanted instead.
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=Output
OutputBaseFilename=PulsemapSetup-{#MyAppVersion}
SetupIconFile=src\Pulsemap.App\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
LicenseFile=LICENSE
; Unsigned for now — no hard install block under InnoSetup (unlike MSIX), just a SmartScreen
; warning until download reputation builds. See ADR-0002's "Consequences" section.

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
