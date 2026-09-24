; Inno Setup script for Calc Pro (WPF).
; Bundles the single-file self-contained build produced by build.ps1.
;
; Compile (build.ps1 does this for you):
;   ISCC.exe /DMyAppVersion=1.4.0 /DSourceExe=<path\CalcPro.exe> /DOutputDir=<dist> installer\CalcPro.iss

#ifndef MyAppVersion
  #define MyAppVersion "1.4.0"
#endif
#ifndef SourceExe
  #define SourceExe "..\..\dist\.stage\calcpro-wpf\CalcPro.exe"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\dist"
#endif

#define MyAppName "Calc Pro"
#define MyAppPublisher "ALEXalesha"
#define MyAppExeName "CalcPro.exe"
#define AppIcon "..\src\CalcPro.Wpf\Resources\app.ico"

[Setup]
; AppId is kept from 1.0.0 so the new setup upgrades an existing install in place.
AppId={{8F3A6B21-4C5D-4E7A-9B12-CALCPRO00001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile={#AppIcon}
OutputDir={#OutputDir}
OutputBaseFilename=CalcPro-{#MyAppVersion}-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0
CloseApplications=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
