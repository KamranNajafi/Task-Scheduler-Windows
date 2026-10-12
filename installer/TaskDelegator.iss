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
#define AppGitHub "https://github.com/KamranNajafi"
#define AppRepo "https://github.com/KamranNajafi/Task-Scheduler-Windows"

[Setup]
AppId={{B3F2A1C4-7E9D-4A52-9C3B-7F1E2D6A5B40}}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppGitHub}
AppSupportURL={#AppRepo}
AppUpdatesURL={#AppRepo}
AppComments=Created by {#AppPublisher} — {#AppRepo}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup {#AppVersion}
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
UninstallDisplayName={#AppName} {#AppVersion}

[Messages]
; Show the app name/version and the author + GitHub repository on the first wizard page.
WelcomeLabel2=This will install [name/ver] on your computer.%n%nCreated by {#AppPublisher}%n{#AppRepo}%n%nIt is recommended that you close all other applications before continuing.

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

[Code]
// Registry key Inno Setup writes for this product (AppId + "_is1").
const
  UninstallKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{B3F2A1C4-7E9D-4A52-9C3B-7F1E2D6A5B40}_is1';

function GetUninstallString(): String;
var
  s: String;
begin
  Result := '';
  if RegQueryStringValue(HKLM, UninstallKey, 'UninstallString', s) then
    Result := s
  else if RegQueryStringValue(HKLM64, UninstallKey, 'UninstallString', s) then
    Result := s;
end;

// True when this run was started by a newer installer upgrading us (see PrepareToInstall).
function UpgradeFlagPresent(): Boolean;
var
  i: Integer;
begin
  Result := False;
  for i := 1 to ParamCount do
    if CompareText(ParamStr(i), '/UPGRADEUNINSTALL') = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

// Clean upgrade: silently remove any previously installed version BEFORE installing the
// new one, so no old files are left behind. We pass /UPGRADEUNINSTALL so the old
// uninstaller KEEPS the user's delegations (only a real uninstall wipes them).
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  unst: String;
  rc: Integer;
begin
  Result := '';
  unst := GetUninstallString();
  if unst <> '' then
  begin
    unst := RemoveQuotes(unst);
    Exec(unst, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /UPGRADEUNINSTALL', '',
         SW_HIDE, ewWaitUntilTerminated, rc);
  end;
end;

// Complete uninstall: on a genuine (user) uninstall, run the app's cleanup so every
// delegation, the Task Scheduler folder, and C:\ProgramData\AppDelegation are removed —
// nothing left behind. On an upgrade (/UPGRADEUNINSTALL) we keep the delegations.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  rc: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    if not UpgradeFlagPresent() then
      Exec(ExpandConstant('{app}\{#AppExe}'), '--uninstall-clean', '',
           SW_HIDE, ewWaitUntilTerminated, rc);
  end;
end;
