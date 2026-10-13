#define AppName "QuickMacro"
#define AppVersion "1.0.0"
#define AppExe "QuickMacro.exe"

[Setup]
AppId={{7DBE2C49-6F12-43D7-9A56-7050B15D8E0C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=QuickMacro
DefaultDirName={localappdata}\Programs\QuickMacro
DefaultGroupName=QuickMacro
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=..\artifacts\installer
OutputBaseFilename=QuickMacro-Setup-{#AppVersion}-x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Unsigned by default. SignTool directives should only be added when a valid certificate is available.

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\QuickMacro"; Filename: "{app}\{#AppExe}"
Name: "{userdesktop}\QuickMacro"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch QuickMacro"; Flags: nowait postinstall skipifsilent
