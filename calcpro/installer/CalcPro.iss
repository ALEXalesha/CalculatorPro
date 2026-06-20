; Inno Setup script for Calc Pro
; Bundles the single-file self-contained build (dist\portable\CalcPro.exe).
; Compile:  ISCC.exe installer\CalcPro.iss   (run from repo root)

#define MyAppName "Calc Pro"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Alexey"
#define MyAppExeName "CalcPro.exe"
; Paths are resolved relative to this .iss file (installer\), so step up one level.
#define RepoRoot ".."
#define SourceExe RepoRoot + "\dist\portable\" + MyAppExeName
#define AppIcon RepoRoot + "\CalcPro.Wpf\Resources\app.ico"

[Setup]
AppId={{8F3A6B21-4C5D-4E7A-9B12-CALCPRO00001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile={#AppIcon}
OutputDir={#RepoRoot}\dist\setup
OutputBaseFilename=CalcPro-{#MyAppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0

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
