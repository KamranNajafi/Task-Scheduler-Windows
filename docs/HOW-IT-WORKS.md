# How it works — technical detail

This tool implements **privileged application delegation** that works for standard
(non-admin) users, with the elevated application visible on the user's own desktop.
Every step uses a documented Windows API and is gated on an administrator having
authenticated via UAC first. Nothing here circumvents a Windows security boundary.

## Why a SYSTEM launcher is needed

A scheduled task that merely runs under a stored admin identity *does* elevate, but
its GUI lands in that identity's session — not the standard user's — because of
Windows session isolation. To make an **elevated** app appear on a **standard
user's** interactive desktop, something running as **SYSTEM** must create the
process **into that user's session** with an elevated token. That is exactly what
this tool does.

## Components

```
Administrator                     TaskDelegator.exe (GUI, elevated)        Standard user
     |  run exe ------------------------>  self requires admin (UAC)             |
     |  authenticate at UAC ------------>  configuring now                       |
     |  pick app + user ---------------->  copy exe to ProgramData               |
     |                                     write descriptor (admin-only ACL)     |
     |                                     register SYSTEM task \AppDelegation\  |
     |                                       RunLevel = Highest                  |
     |                                       action = TaskDelegator.exe --launch |
     |                                       SDDL grants user GR+GX (run)        |
     |                                     create desktop shortcut ------------> |
     |                                                                           | double-click
     |                                                                           |  schtasks /run (hidden)
     |     SYSTEM task fires -> TaskDelegator.exe --launch "<task>"              |
     |       find interactive session (WTS)                                      |
     |       build elevated primary token (SYSTEM dup, or LogonUser for account) |
     |       SetTokenInformation(TokenSessionId = user session)                  |
     |       CreateProcessAsUser(..., lpDesktop = "winsta0\\default") ---------> | app opens
     |                                                                           |  elevated, visible
```

### 0. Three modes (one exe)
The manifest is `asInvoker` so a standard user can open the launcher with no UAC
prompt. `Program.Main` routes by arguments:
- **no args** → `StudentLauncherForm` — run-only launcher for the standard user.
- **`--admin`** → `MainForm` (admin console); if not already elevated it relaunches
  itself with the `runas` verb (UAC) = the "admin login".
- **`--launch "<task>"`** → `SessionLauncher` (runs as SYSTEM from the task).

### 1. Admin gate
Configuration lives only in the admin console (`--admin`), which self-elevates.
Students cannot reach it without administrator credentials at the UAC prompt. The
SYSTEM task invokes the exe with `--launch`; SYSTEM is already elevated regardless
of the manifest.

### 1b. Student launcher (run-only)
When a delegation is created, a student-readable **menu entry** (friendly name +
task name only — no secrets) is written to `%ProgramData%\AppDelegation\menu\`
(Users may read; only SYSTEM/Administrators may write). The launcher lists the
current user's entries and, on run, executes `schtasks /run /tn "\AppDelegation\…"`
for that one task. It can only *run* pre-approved tasks — it never creates,
changes, removes, or uninstalls anything.

### 2. Inventory
- **Software** (`SoftwareInventory`): reads the uninstall registry keys (HKLM 64/32,
  HKCU), resolves each app's main `.exe` from `DisplayIcon`/`InstallLocation`.
- **Users** (`UserInventory`): WMI `Win32_UserAccount` for local accounts;
  `NetLocalGroupGetMembers` (resolved from the well-known Administrators SID, so it
  is locale-safe) to flag admins.

### 3. Register the delegation (`DelegationManager`)
Using the Task Scheduler API, under `\AppDelegation\`:

| Setting | Value |
|---|---|
| Principal | `SYSTEM`, `LogonType = ServiceAccount`, `RunLevel = Highest` |
| Action | `TaskDelegator.exe --launch "<taskName>"` (the stable ProgramData copy) |
| Triggers | none (on-demand only) |
| SDDL | `O:BAG:BAD:(A;;GA;;;BA)(A;;GA;;;SY)(A;;GRGX;;;<userSID>)` |

The descriptor (which app, which mode, which user) is written to
`%ProgramData%\AppDelegation\delegations\<task>.json`.

### 4. Hardened storage
`DelegationManager.Harden` (via `icacls`) breaks inheritance and sets:
- `DataDir`: SYSTEM + Administrators full; Users read/execute (for the `.vbs` and exe).
- `delegations\` and `creds\`: **SYSTEM + Administrators only.** Standard users
  cannot read credential blobs or tamper with descriptors (a descriptor drives what
  runs as SYSTEM, so this is a security boundary).

### 5. The SYSTEM launcher (`SessionLauncher` + `NativeMethods`)
On `--launch <task>` (running as SYSTEM):
1. Load and validate the descriptor.
2. `GetTargetSessionId` — enumerate sessions (`WTSEnumerateSessions`), pick the
   active session that has a logged-on user (console first, else first active RDP
   user).
3. Build an elevated **primary** token:
   - **SYSTEM mode:** `OpenProcessToken` + `DuplicateTokenEx` of our own SYSTEM token.
   - **Account mode:** `LogonUser` + `TokenLinkedToken` (full elevated token) +
     `DuplicateTokenEx`.
4. `SetTokenInformation(TokenSessionId)` → move the token into the user's session
   (requires `SeTcbPrivilege`, which SYSTEM holds).
5. `CreateEnvironmentBlock`, `STARTUPINFO.lpDesktop = winsta0\default`,
   `CreateProcessAsUser(...)` → the app starts elevated, on the user's desktop.

### 6. Desktop shortcut
A hidden launcher `%ProgramData%\AppDelegation\run_<task>.vbs` runs
`schtasks /run /tn "\AppDelegation\<task>"` with a hidden window; the `.lnk` on the
user's desktop points at it with the app's icon.

### 7. Portability
The GUI runs from anywhere. On first delegation it copies itself (a single
self-contained exe) to `%ProgramData%\AppDelegation\TaskDelegator.exe` and points the
task at that stable path, so delegations survive moving or deleting the portable copy.

## Audit
`audit.log` records every create/remove (admin, app, mode, target user).
`launcher.log` records each triggered launch (session, mode, success/error).
