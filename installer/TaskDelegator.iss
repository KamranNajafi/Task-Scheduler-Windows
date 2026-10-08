; Inno Setup script for Task Delegator (Windows 10/11, net8 build).
; Installs the app to Program Files and, if the Microsoft Visual C++ runtime is
; missing, silently runs the bundled vc_redist.x64.exe — so the setup is fully
; self-contained and needs no pre-installed prerequisites.
;
; CI passes the version:  ISCC /DAppVersion=1.2.0 installer\TaskDelegator.iss

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
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}

[Tasks]
Name: "desktopicon"; Description: "Create an 'Allowed Programs' shortcut on the desktop"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "..\publish\net8\TaskDelegator.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\redist\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\Allowed Programs"; Filename: "{app}\{#AppExe}"; Comment: "Run approved applications (student launcher)"
Name: "{group}\Task Delegator (Administrator)"; Filename: "{app}\{#AppExe}"; Parameters: "--admin"; Comment: "Configure delegations (requires administrator)"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Allowed Programs"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
; One-time prerequisite: install the Visual C++ runtime only if it is missing.
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing the Microsoft Visual C++ runtime (one-time prerequisite)..."; Check: VCRedistMissing
; Offer to open the admin console right after installing.
Filename: "{app}\{#AppExe}"; Parameters: "--admin"; Description: "Open Task Delegator (Administrator) now"; Flags: postinstall nowait skipifsilent

[Code]
function VCRedistMissing: Boolean;
begin
  // The x64 VC++ 2015-2022 runtime installs these into System32.
  Result := not (FileExists(ExpandConstant('{sys}\vcruntime140.dll')) and
                 FileExists(ExpandConstant('{sys}\vcruntime140_1.dll')));
end;
