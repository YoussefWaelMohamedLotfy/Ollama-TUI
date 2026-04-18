; Ollama-TUI InnoSetup Installer Script
; Bundles the NativeAOT self-contained publish output for Windows.

#define AppName      "Ollama-TUI"
#define AppVersion   "1.0.0"
#define AppPublisher "YoussefWaelMohamedLotfy"
#define AppURL       "https://github.com/YoussefWaelMohamedLotfy/Ollama-TUI"
#define AppExeName   "Ollama.TUI.exe"

; PublishDir is relative to this .iss file's location (installer/).
; Override on the command line with /DPublishDir=<path> if needed.
#ifndef PublishDir
  #define PublishDir "..\nativeaot-publish"
#endif

[Setup]
AppId={{A3F8E2C1-4B7D-4E9A-B5F3-2D1C8A6E9F02}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\installer-output
OutputBaseFilename=Ollama-TUI-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern dynamic
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";                        Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}";  Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";                  Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; \
  Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; \
  Flags: nowait postinstall skipifsilent
