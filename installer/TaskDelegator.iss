; Inno Setup script for Task Delegator.
;
; Installs the .NET Framework 4.8 (net48) build — it uses the in-box .NET
; Framework present on Windows 10/11 and has NO Visual C++ runtime dependency,
; so it avoids the "side-by-side configuration is incorrect" error that the
; self-contained net8 build can hit on clean lab PCs.
;
; CI passes the version:  ISCC /DAppVersion=1.3.0 installer\TaskDelegator.iss

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppName "Task Delegator"
#define AppExe "TaskDelegator.exe"
#define AppPublisher "Kamran Najafi"

[Setup]
AppId={{B3F2A1C4-7E9D-4A52-9C3B-7F1E2D6A5B40}}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\TaskDelegator
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\installer_out
OutputBaseFilename=TaskDelegatorSetup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
MinVersion=6.1sp1
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}

[Tasks]
Name: "desktopicon"; Description: "Create an 'Allowed Programs' shortcut on the desktop"; GroupDescription: "Additional shortcuts:"

[Files]
; The whole net48 output folder (TaskDelegator.exe + its dependency DLLs).
Source: "..\publish\net48\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Allowed Programs"; Filename: "{app}\{#AppExe}"; Comment: "Run approved applications (student launcher)"
Name: "{group}\Task Delegator (Administrator)"; Filename: "{app}\{#AppExe}"; Parameters: "--admin"; Comment: "Configure delegations (requires administrator)"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Allowed Programs"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "--admin"; Description: "Open Task Delegator (Administrator) now"; Flags: postinstall nowait skipifsilent
