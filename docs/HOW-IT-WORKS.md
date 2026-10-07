# How it works — technical detail

This tool implements **privileged application delegation** using the Windows Task
Scheduler. Nothing here circumvents a Windows security boundary; every step uses a
documented API, and every step is gated on an administrator having already
authenticated via UAC.

## The flow

```
Administrator                       This tool (elevated)                 Standard user
     |                                      |                                  |
     | run tool ------------------------->  |  self-elevates (UAC prompt)      |
     | authenticate at UAC  ------------->  |  now running as admin            |
     | pick app + user + run-as creds --->  |                                  |
     |                                      |  Register on-demand task         |
     |                                      |   \AppDelegation\Delegated_*      |
     |                                      |   RunLevel = HIGHEST              |
     |                                      |   LogonType = PASSWORD (stored)   |
     |                                      |   SDDL grants user GR+GX (run)    |
     |                                      |  Create desktop shortcut -------> |
     |                                      |                                  | double-click shortcut
     |                                      |                                  |  wscript -> schtasks /run
     |                                      |  Task Scheduler launches app  <---|
     |                                      |   elevated, as stored admin       |
```

## Components

### 1. Self-elevation (`Test-IsAdmin` / `Invoke-SelfElevate`)
On launch the tool checks whether it holds the Administrators role. If not, it
relaunches itself with the `runas` verb, producing the standard UAC prompt. A
standard user cannot get past this, so **only an administrator can create
delegations**. This is the tool's authentication gate.

### 2. Enumerate installed software (`Get-InstalledSoftware`)
Reads the uninstall registry keys:
- `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*`
- `HKLM\SOFTWARE\WOW6432Node\...\Uninstall\*`
- `HKCU\...\Uninstall\*`

It filters out OS updates/components and resolves each app's main executable from
`DisplayIcon` or `InstallLocation`. The admin can always override with **Browse…**.

### 3. Enumerate local users (`Get-LocalUsersInfo`)
Uses `Get-LocalUser` (falling back to `Win32_UserAccount`) and cross-references
`Administrators` group membership so the UI can show **Admin** vs **Standard**.

### 4. Register the delegated task (`New-DelegatedTask`)
Via the `Schedule.Service` COM API, under folder `\AppDelegation\`:

| Setting | Value | Why |
|---|---|---|
| `Principal.RunLevel` | `TASK_RUNLEVEL_HIGHEST` (1) | The app runs elevated. |
| `Principal.LogonType` | `TASK_LOGON_PASSWORD` (1) | Runs as a stored admin identity, independent of who triggers it. |
| Triggers | *none* | On-demand only — runs only when explicitly launched. |
| `AllowDemandStart` | `true` | Lets the user start it via `schtasks /run`. |
| Security descriptor | `O:BAG:BAD:(A;;GA;;;BA)(A;;GA;;;SY)(A;;GRGX;;;<userSID>)` | Admins/SYSTEM full control; the delegated user gets **Generic Read + Generic Execute** — enough to *run* the task and nothing else. |

The run-as administrator credentials are passed to `RegisterTaskDefinition` and
stored by Windows (LSA secret). They are **not** written to disk by this tool and
are **not** retrievable by the standard user.

### 5. Desktop shortcut (`New-DelegatedShortcut`)
Writes a tiny hidden launcher to
`C:\ProgramData\AppDelegation\run_<task>.vbs`:

```vbscript
CreateObject("WScript.Shell").Run "schtasks /run /tn ""\AppDelegation\<task>""", 0, False
```

and a `.lnk` on the target user's desktop pointing at
`wscript.exe //B //Nologo "<vbs>"`, with the app's icon. The `0` window style
means the `schtasks` call runs with no visible console window.

### 6. Manage / remove (`Get-Delegations` / `Remove-Delegation`)
Lists tasks under `\AppDelegation\` and removes a delegation by deleting the task,
its launcher, and any desktop shortcuts that reference it.

## Session visibility note

Because the task runs as a stored admin identity (`LogonType = PASSWORD`), the app
is launched in that identity's security context. For most line-of-business apps the
window appears for the interactive user who triggered it. If you delegate an app
whose UI must render in a very specific session and you observe session-isolation
behavior, register the task with an interactive logon type instead — the mechanism
is otherwise identical. The default (stored password) is chosen because it most
directly satisfies "runs elevated without the user knowing the password."

## Audit

Every create and remove is appended to `C:\ProgramData\AppDelegation\audit.log`
with timestamp, the admin who performed it, the app, the run-as account, and the
target user.
