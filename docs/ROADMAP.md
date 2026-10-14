# Task Delegator — Roadmap & Developer Handoff

> **Read this first.** This is the single source of truth for *where the project is*,
> *what comes next*, and *the rules every change must follow*. It is written so that a
> new contributor — or a new Claude Code session with no chat history — can continue the
> work safely.
>
> **To start a new session:** _"Read `docs/ROADMAP.md` (and `docs/HOW-IT-WORKS.md`,
> `docs/SECURITY.md`), then continue with Phase N."_
>
> A full Persian summary is at the end — [خلاصهٔ فارسی](#خلاصهٔ-فارسی--persian-summary).

| | |
|---|---|
| **Last updated** | 2026-10-14 |
| **Current version** | `v1.14.0` (see [`VERSION`](../VERSION)) |
| **Current phase** | **Phase 1 — Windows 7 completion** (versions stay `1.x.x`) |
| **Next phase** | Phase 2 → `v2.0.0` — **starts only after Phase 1 is complete** |
| **Owner / author** | Kamran Najafi — <https://github.com/KamranNajafi> |

---

## Table of contents

1. [Status snapshot](#1-status-snapshot)
2. [Project at a glance](#2-project-at-a-glance)
3. [Repository map](#3-repository-map)
4. [Build, CI and release process](#4-build-ci-and-release-process)
5. [Architecture notes (beyond HOW-IT-WORKS)](#5-architecture-notes-beyond-how-it-works)
6. [Decisions log](#6-decisions-log)
7. [Lessons learned — pitfalls we already hit](#7-lessons-learned--pitfalls-we-already-hit)
8. [Compatibility rules (Windows 7 / .NET Framework 4.8) — mandatory](#8-compatibility-rules-windows-7--net-framework-48--mandatory)
9. [Phase 1 — Windows 7 completion (`1.x.x`)](#9-phase-1--windows-7-completion-1xx)
10. [Phase 2 — Version 2 backlog (`v2.0.0`)](#10-phase-2--version-2-backlog-v200)
11. [Versioning and branching policy](#11-versioning-and-branching-policy)
12. [Working conventions](#12-working-conventions)
13. [Open questions](#13-open-questions)
14. [خلاصهٔ فارسی — Persian summary](#خلاصهٔ-فارسی--persian-summary)

---

## 1. Status snapshot

### Works and verified by the owner (Windows 10/11)

- Admin console: lists installed software and local users, creates and removes delegations
  (SYSTEM mode and Admin-account mode).
- Student "Allowed Programs" launcher: run-only, launches the delegated app elevated on the
  student's own desktop, **no black console window** (fixed in v1.8.1).
- Version shown in title bars, top header, installer and Add/Remove Programs.
- Creator credit + GitHub/repository links in both windows; custom licence.
- **Fixes tab**: one-click SQL Server Error 262 fix (grant `dbcreator`/`sysadmin` to
  `NT AUTHORITY\SYSTEM`), optional SQL login (e.g. `sa`), result verified with
  `IS_SRVROLEMEMBER` (v1.14.0).
- Complete uninstall (Control Panel removes tasks, data and shortcuts) and clean upgrade
  (old version removed first, delegations kept) — v1.11.0.
- Application icon — v1.13.0.

### Built and released, but **not yet tested** by the owner

- **Network tab** (v1.12.0, layout fixed in v1.14.0): silent-install command + `.bat`,
  Group Policy startup script, workgroup remote push. → Covered by the Phase 1 test plan.

### Not tested at all

- **Anything on Windows 7** (installer, net48 zip, admin console, launcher, SQL fix, network
  push, upgrade/uninstall, PowerShell helper on PowerShell 2.0). → Phase 1.

### Known gaps (planned)

- The installer does **not** check for or install **.NET Framework 4.8** (needed on Win 7).
- The Network tab installs the **program** only — **delegations must still be created on
  each PC** (see Phase 2, item 2.2 Export/Import).
- Workgroup push has known weaknesses (see Phase 2, item 2.1).

---

## 2. Project at a glance

**Purpose.** In a training lab/classroom, an administrator lets a **standard (non-admin)
Windows user** run **specific approved applications with administrator rights**, without
ever giving out the admin password and without letting the user uninstall or reconfigure
anything. It is the Windows equivalent of a `sudo NOPASSWD` rule for one command, built on
the sanctioned **Task Scheduler + session APIs** — **not** a UAC bypass or exploit.

**One executable, four modes** (`src/TaskDelegator/Program.cs`):

| Invocation | Mode | Runs as |
|---|---|---|
| `TaskDelegator.exe` | Run-only **student launcher** ("Allowed Programs") | the logged-on user, no UAC |
| `TaskDelegator.exe --admin` | **Admin console** (self-elevates via UAC `runas`) | administrator |
| `TaskDelegator.exe --launch "<task>"` | **SYSTEM launcher** — invoked only by the scheduled task | SYSTEM |
| `TaskDelegator.exe --uninstall-clean` | Full cleanup — invoked only by the uninstaller | administrator |

**Flow of a delegated launch.** Student clicks shortcut/launcher → `schtasks /run` on a task
the student may *run* (task SDDL grants read+execute only) → Task Scheduler runs
`TaskDelegator.exe --launch "<task>"` as SYSTEM → `SessionLauncher` reads the admin-only
descriptor → builds a SYSTEM (or admin-account) primary token → `CreateProcessAsUser` into
the student's interactive session on `winsta0\default`.

Detailed design: [`HOW-IT-WORKS.md`](HOW-IT-WORKS.md). Threat model and limits:
[`SECURITY.md`](SECURITY.md). End-user guide (EN+FA, also released as PDF):
[`USER-GUIDE.md`](USER-GUIDE.md) / [`user-guide.html`](user-guide.html).

---

## 3. Repository map

| Path | Responsibility |
|---|---|
| `VERSION` | **Single source of truth** for the version (e.g. `v1.14.0`). |
| `src/TaskDelegator/TaskDelegator.csproj` | Multi-target `net8.0-windows;net48`, WinForms, `NoWin32Manifest`, reads `VERSION`, `ApplicationIcon`. |
| `src/TaskDelegator/Program.cs` | Entry point; dispatches the four modes; elevation (`runas`) and DPI set in code. |
| `src/TaskDelegator/Ui/MainForm.cs` | Admin console: tabs **Create delegation**, **Existing delegations**, **Fixes**, **Network**; header/credits/status bar. |
| `src/TaskDelegator/Ui/StudentLauncherForm.cs` | Run-only student launcher. |
| `src/TaskDelegator/Services/DelegationManager.cs` | Create/list/remove delegations; hardening (icacls); task registration with SDDL; shortcuts; `RemoveAll()` for full uninstall. |
| `src/TaskDelegator/Services/SessionLauncher.cs` | The `--launch` path (runs as SYSTEM). |
| `src/TaskDelegator/Native/NativeMethods.cs` | P/Invoke: tokens, WTS session lookup, `CreateProcessAsUser`, admin-group SIDs, PE-subsystem check (`IsGuiExecutable`). |
| `src/TaskDelegator/Services/StudentMenu.cs` | Lists the current user's entries; triggers `schtasks /run`. |
| `src/TaskDelegator/Services/SoftwareInventory.cs` | Installed apps from the Uninstall registry keys (64/32-bit + HKCU). |
| `src/TaskDelegator/Services/UserInventory.cs` | Local users via WMI `Win32_UserAccount`, admin flag via `NetLocalGroupGetMembers`. |
| `src/TaskDelegator/Services/CredentialStore.cs` | Admin-account password, **DPAPI LocalMachine** + app entropy (machine-bound — cannot be exported). |
| `src/TaskDelegator/Services/SqlServerFix.cs` | Fixes tab: find local SQL instances, grant role via `sqlcmd` (`-E` or `-U/-P`), verify. |
| `src/TaskDelegator/Services/NetworkDeploy.cs` | Network tab: silent command, deploy `.bat`, GPO startup script, workgroup push (`net use` + copy + `schtasks /s`). |
| `src/TaskDelegator/Services/Paths.cs` | All well-known paths (`C:\ProgramData\AppDelegation\…`), task folder `\AppDelegation`, prefix `Delegated`. |
| `src/TaskDelegator/Services/AuditLog.cs` | `audit.log` (admin actions) and `launcher.log` (SYSTEM launcher). Never throws. |
| `src/TaskDelegator/Services/AppInfo.cs` | Product name, author, URLs, version (from `AssemblyInformationalVersion`). |
| `src/TaskDelegator/Models/Models.cs` | `DelegationConfig` (descriptor), `MenuEntry`, `RunAsMode`, inventory models. |
| `src/TaskDelegator/app.ico` | Icon (shield + keyhole), 16–256 px, BMP entries for small sizes. |
| `installer/TaskDelegator.iss` | Inno Setup: installs the **net48** build; `[Code]` for clean upgrade and full uninstall. |
| `tools/Grant-SqlCreateDatabase.ps1` | Command-line alternative to the Fixes tab SQL button. |
| `.github/workflows/build.yml` | CI: build both targets, zip, installer, PDF, artifacts, release. |
| `docs/` | `HOW-IT-WORKS.md`, `SECURITY.md`, `USER-GUIDE.md` + `user-guide.html` (→ PDF), this file. |
| `LICENSE` | Custom source-available, **non-commercial, attribution-required** licence (EN+FA). |

**Runtime data** (created on the target PC): `C:\ProgramData\AppDelegation\`
— `delegations\` (descriptors, admin-only), `creds\` (DPAPI blobs, admin-only), `menu\`
(student-readable entries), `audit.log`, `launcher.log`, `run_<task>.vbs`,
`TaskDelegator.exe` (stable copy used by the tasks; net48 also copies its DLLs).
Scheduled tasks live in the Task Scheduler folder **`\AppDelegation`**, named
`Delegated_<friendly>_<user>`.

---

## 4. Build, CI and release process

**There is no Windows machine in the cloud development session.** Code cannot be run or
UI-tested there; **CI is the compiler** and **the owner is the tester**.

`.github/workflows/build.yml` (runs on every push, `windows-latest`):

1. Reads `VERSION` → `APPVER` (numeric, `1.14.0`) and `APPVER_V` (`v1.14.0`).
2. `dotnet publish` **net8** — `win-x64`, self-contained, single file → `TaskDelegator.exe` (~72 MB, Win 10/11 only).
3. `dotnet publish` **net48** → folder, zipped as `TaskDelegator-win7-net48.zip`.
4. `choco install innosetup` (**latest** — see Phase 1, item 1.3) → `ISCC /DAppVersion=…` → `TaskDelegatorSetup.exe` (net48 payload).
5. Headless Edge prints `docs/user-guide.html` → `TaskDelegator-User-Guide.pdf` (poll loop waits for the async write).
6. Uploads artifacts.
7. **Release:** only if the head commit message contains **`[release]`** (or a `v*` tag / manual dispatch). The tag is read from `VERSION`; `softprops/action-gh-release` publishes the 4 assets.

**How to ship a release**

1. Bump `VERSION` (e.g. `v1.15.0`).
2. If docs changed: edit **both** `docs/USER-GUIDE.md` and `docs/user-guide.html`
   (EN + FA), and check the HTML is well-formed (any HTML parser; CI turns it into the PDF).
3. Commit with `[release]` in the message; push.
4. Wait for the run to finish; confirm **conclusion = success** and that the release has
   **4 assets**. A red run means **no release** — fix and push again.

**Version stamping.** The csproj reads `VERSION` itself (so a plain local build is right);
CI additionally passes `-p:Version` / `-p:InformationalVersion`. `AppInfo.Version` shows it.

---

## 5. Architecture notes (beyond HOW-IT-WORKS)

- **Launching without a console window.** `NativeMethods.LaunchInSession` passes
  `CREATE_NEW_CONSOLE` **only** to console (CUI) programs; GUI programs get none. The
  subsystem is read from the PE header (`IsGuiExecutable`; unreadable ⇒ treated as GUI).
  The decision is logged as `subsystem=GUI|console` in `launcher.log`.
- **Fixes tab (SQL Server Error 262).** A delegated SSMS runs as `NT AUTHORITY\SYSTEM`,
  which is **not** `sysadmin` by default since SQL Server 2012. `SqlServerFix` discovers
  local instances (`HKLM\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL`),
  runs `CREATE LOGIN … / ALTER SERVER ROLE … ADD MEMBER …` via `sqlcmd` as the current
  Windows admin (`-E`) or a SQL login (`-U/-P`), then verifies with `IS_SRVROLEMEMBER`.
  The connecting identity **must itself be a SQL sysadmin**. SSMS must **reconnect**
  afterwards. Without `sqlcmd`, the T-SQL is shown with a Copy button.
- **Network tab.** Silent args are `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`. The GPO
  startup script writes `%ProgramData%\AppDelegation\installed-version.txt` and skips the
  install when it matches. The workgroup push does: `net use \\host\admin$` → copy installer
  to `\\host\admin$\Temp` → `schtasks /s host /create … /ru SYSTEM` → `schtasks /run` →
  `net use /delete`. It runs on a background thread and reports "install triggered", **not**
  "installed".
- **Upgrade vs uninstall** (`installer/TaskDelegator.iss`, `[Code]`):
  `PrepareToInstall` reads the previous `UninstallString`
  (`…\Uninstall\{B3F2A1C4-7E9D-4A52-9C3B-7F1E2D6A5B40}_is1`, HKLM then HKLM64) and runs it
  with `/VERYSILENT … /UPGRADEUNINSTALL`. `CurUninstallStepChanged(usUninstall)` runs
  `TaskDelegator.exe --uninstall-clean` **only when `/UPGRADEUNINSTALL` is absent**.
  ⇒ upgrade keeps delegations; Control-Panel uninstall removes everything. Deleting the
  install folder by hand cleans nothing.
- **UI layout rule.** The Fixes and Network tabs use **absolute positioning**
  (`Location`/`Size`, helper `Lbl(...)`). Do **not** put docked/AutoSize panels inside
  AutoSize `GroupBox`es — that collapsed and overlapped in v1.12–1.13.

---

## 6. Decisions log

| # | Decision | Why |
|---|---|---|
| D1 | Elevation via **Task Scheduler task running as SYSTEM** + a launcher that starts the app in the user's session. | Sanctioned mechanism; standard users can be given *run* rights on one task only (SDDL). No UAC bypass. |
| D2 | **Two builds:** net48 (shipping, Win 7 SP1+) and net8 (portable single file, Win 10/11). | net48 uses the in-box framework on Win 10/11 and supports Win 7; net8 is a convenience build. |
| D3 | **No embedded Win32 manifest** (`NoWin32Manifest`); elevation (`runas`) and DPI in code. | The embedded manifest caused "side-by-side configuration is incorrect" (`14001`) on some PCs. |
| D4 | **Installer = Inno Setup, installs net48.** | No VC++ runtime dependency (the net8 self-contained build needs it). |
| D5 | **`VERSION` file is the single source of truth.** | One edit updates exe metadata, UI, installer and release tag. |
| D6 | **Release only via `[release]` commit marker.** | Ordinary pushes build and test; only intentional commits publish. |
| D7 | **Absolute layout** for tool tabs. | Deterministic in WinForms; we cannot UI-test in the cloud session. |
| D8 | **Upgrade preserves delegations; uninstall wipes everything.** | Owner requirement: clean removal, painless updates. |
| D9 | **Custom non-commercial, attribution-required licence.** | Owner requirement: free to develop, no selling without permission, credit in repo *and* in the program. |
| D10 | **Versions stay `1.x.x` until Windows 7 is complete; `v2.0.0` only after.** | Owner decision (2026-10-14). |
| D11 | **Prerequisites for Win 7: one Inno script, two setups** (normal + offline). *Status: proposed — confirm after the first Win 7 test.* | See Phase 1, item 1.2. |

---

## 7. Lessons learned — pitfalls we already hit

1. **`TaskService.GetFolder` returns `null`** (does not throw) for a missing folder → NRE on the
   first delegation. Use `GetOrCreateFolder` / `TryGetFolder`.
2. **Embedded manifest → SxS error 14001** on some Windows installs (D3).
3. **net8 self-contained needs the VC++ Redistributable** on clean PCs → ship net48 (D4).
4. **Quoted paths from the registry** (`"C:\…\app.exe"`, `,0` icon index, `%vars%`) fail
   `File.Exists` → `NormalizeExePath`.
5. **`CREATE_NEW_CONSOLE` on a GUI app** → empty black window that looks like a hang.
6. **AutoSize `GroupBox` + docked children** → controls overlap (D7).
7. **Invalid Inno directive** (`AppContactURL`) broke the build → check every directive
   against the Inno Setup documentation.
8. **WinForms APIs missing in .NET Framework 4.8** (e.g. `TextBox.PlaceholderText`) → the
   net48 build fails. CI catches compile errors, so build both targets.
9. **MSBuild property functions:** don't rely on `Regex`; use `String` methods.
10. **Headless Edge writes the PDF asynchronously** → the CI step polls until the size is stable.
11. **`AuditLog` after `RemoveAll()`** would recreate `C:\ProgramData\AppDelegation` → never
    log after deleting the data dir.
12. **Upgrades must not wipe delegations** → `/UPGRADEUNINSTALL` flag (D8).
13. **SQL Server:** SYSTEM is not sysadmin; grants need a sysadmin connection; SSMS must
    reconnect. A failed grant usually means the connecting account is not a SQL admin.
14. **Icons:** small sizes as BMP entries (built with ImageMagick) for best compatibility.
15. **Cloud session has no Windows:** prefer defensive code, rich logging
    (`launcher.log`, `audit.log`) and ask the owner for logs/screenshots.

---

## 8. Compatibility rules (Windows 7 / .NET Framework 4.8) — mandatory

Every change — in Phase 1 **and** Phase 2 — must keep the **net48 build working on
Windows 7 SP1 (x86 and x64)**, because that is what the installer ships.

- **Compile for both targets.** No API that exists only in newer .NET / WinForms. CI's
  net48 build is the gate for *compile-time* problems.
- **No Windows 8+ only APIs** in P/Invoke or COM. Check minimum OS in the API docs.
- **x86 matters.** Old labs often run 32-bit Win 7. No 64-bit-only assumptions
  (`IntPtr` sizes, `Program Files` paths, registry views).
- **TLS:** if any feature calls the internet (e.g. update check), set
  `ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12` in the net48 path
  and fail **gracefully** (labs are often offline).
- **Scripts:** anything run on Win 7 must work on **PowerShell 2.0** (or use `cmd`).
- **Fonts:** Persian UI text on Win 7 should use a font that renders Persian well
  (e.g. **Tahoma**); test it.
- **Installer:** the Inno Setup version used by CI must still support Win 7
  (Phase 1, item 1.3).
- **The net8 `TaskDelegator.exe` does not run on Win 7** (.NET 8 does not support it).
  Never tell Win 7 users to use it.

---

## 9. Phase 1 — Windows 7 completion (`1.x.x`)

**Goal:** the product installs and works fully on Windows 7 SP1 (x86 + x64) from a single
offline download, with the same feature set as on Windows 10/11.
**Gate:** Phase 2 (`v2.0.0`) starts **only** when the exit criteria (9.6) are met.

### 9.1 Test environment

- Virtual machines (VirtualBox / Hyper-V / VMware) — **Windows 7 SP1 x64** and **x86**.
- Take a **snapshot of a clean install** and revert before each run. Useful variants:
  - **A — clean SP1, no updates, no .NET 4.8** (worst case);
  - **B — SP1 + .NET Framework 4.8 installed** (+ updates the .NET installer required).
- One standard (non-admin) user, e.g. `student`, plus the admin account.
- For the network test: at least one Win 7 VM and one Win 10/11 machine on the same network.
- For the SQL test: the SQL Server / SSMS versions the lab really uses on Win 7.

### 9.2 Test checklist (record Pass / Fail + screenshot + `launcher.log` / `audit.log`)

| # | Area | Steps | Expected |
|---|---|---|---|
| T1 | Setup on variant **A** | Run `TaskDelegatorSetup.exe` | *Record what happens* (likely: no .NET 4.8 → app fails to start). Drives item 1.2. |
| T2 | Setup on variant **B** | Run setup | Installs; Start-menu + desktop shortcuts; icon visible; Add/Remove shows "Task Delegator 1.x.x". |
| T3 | Admin console | Open "Task Delegator (Administrator)", approve UAC | Opens; version in title/header; software + users listed; all 4 tabs laid out correctly. |
| T4 | Delegation — SYSTEM | Delegate an app (e.g. Notepad++) to `student` | Success dialog; task in `\AppDelegation`; shortcut on student desktop. |
| T5 | Delegation — Admin account | Same with "Admin account" + password | Success. |
| T6 | Student run | Log on as `student`; run via shortcut **and** via "Allowed Programs" | App opens on the student desktop, elevated, **no black window**; `launcher.log` shows `ok=True subsystem=GUI`. |
| T7 | Student restrictions | As `student`: try "Administrator…" | UAC asks for admin credentials; no way to configure or uninstall. |
| T8 | Remove delegation | Existing delegations → Remove | Task, shortcut and files removed. |
| T9 | Fixes — SQL | Run SSMS delegated; create DB (expect 262); Fixes → Apply (Windows auth, then `sa`) → reconnect | `[OK, verified]`; database creation works. |
| T10 | Fixes — diagnostics | "Open log & data folder" | Explorer opens `C:\ProgramData\AppDelegation`. |
| T11 | Network — command/scripts | Copy command; save deploy `.bat`; save GPO script | Files saved; `.bat` installs silently when run as admin. |
| T12 | Network — push | Push from Win 10 → Win 7 and Win 7 → Win 10 (same local admin) | Record result per host. Expect "Access denied" unless `LocalAccountTokenFilterPolicy=1` (see 2.1). |
| T13 | Upgrade | Install previous release, create a delegation, install new release | Old version removed silently; **delegation still works**. |
| T14 | Uninstall | Control Panel → Uninstall | App, tasks, shortcuts and `C:\ProgramData\AppDelegation` all gone. |
| T15 | Portable zip | Extract `TaskDelegator-win7-net48.zip`, run | Same as T3–T6. |
| T16 | net8 exe | Run `TaskDelegator.exe` (net8) on Win 7 | Expected **not** to run — confirm and document the message. |
| T17 | PowerShell helper | `tools/Grant-SqlCreateDatabase.ps1` on PowerShell 2.0 | Runs or fails clearly — record. |
| T18 | PDF guide | Open `TaskDelegator-User-Guide.pdf` on Win 7 | Persian text renders correctly. |

Also run **T11–T12 on Windows 10/11** — the Network tab has not been tested anywhere yet.

### 9.3 Expected Windows 7 issues (to confirm in testing)

- **.NET Framework 4.8 is not preinstalled** and the setup does not check for it (T1).
- The **.NET 4.8 installer itself may require Windows updates** on an unpatched SP1
  machine — commonly cited: **SHA-2 code-signing support** (KB4474419 + servicing stack
  KB4490628) and the **D3DCompiler update** (KB4019990). *Unverified — confirm on variant A.*
- **Inno Setup:** CI installs the latest version from Chocolatey. Confirm the generated
  setup still starts on Win 7; if not, pin a version that does (item 1.3).
- **SQL Server / SSMS:** newer versions do not install on Win 7; test with what the lab uses.

### 9.4 Phase 1 work items

| ID | Item | Notes |
|---|---|---|
| **1.1** | **Run the test checklist** on v1.14.0 (variants A and B, x64 + x86). | Owner runs; results go into an issue or into this file (§9.7). |
| **1.2** | **Prerequisite handling in the installer** (.NET 4.8 + anything else T1 reveals). | Design below. |
| **1.3** | **Pin Inno Setup** in CI to a version confirmed on Win 7. | e.g. `choco install innosetup --version <x.y.z>`. |
| **1.4** | **Clear asset names / OS table.** | e.g. `TaskDelegator-portable-win10-11.exe` for net8; update README, guide and release notes so Win 7 users never pick it. |
| **1.5** | **Fix Win 7-specific bugs** found in T2–T18. | Keep fixes small; ship as `1.15.x` patches. |
| **1.6** | **Docs:** a "Windows 7" section in the user guide (EN+FA, md+html → PDF). | Prerequisites, which download to use, known limits. |

**1.2 — design (proposed, decision D11).** The owner suggested either one offline setup that
detects Windows 7, or two separate setups. Recommendation — **one Inno script, two builds**:

| Build | Size | Contents | Who uses it |
|---|---|---|---|
| `TaskDelegatorSetup.exe` | ~3 MB | App only. If .NET 4.8 is missing it stops with a clear message + link to the offline setup. | Windows 10/11 (already have .NET 4.8). |
| `TaskDelegatorSetup-Offline.exe` (name TBD, e.g. `-Win7`) | ~120 MB | App + **.NET Framework 4.8 offline redistributable**, installed **only if missing**, before the app. | Windows 7 / offline labs — and it also works on 10/11. |

Implementation notes:

- **Detect the prerequisite, not the OS name.** Check
  `HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full` → `Release` **≥ 528040**
  (= .NET 4.8). This also covers unpatched Win 8.1 / Server machines.
- Same `.iss`, compiled twice: e.g. `ISCC /DOffline=1 …` includes the redistributable in
  `[Files]` (`Check:` function + `Flags: deleteafterinstall`) and runs it silently in
  `[Run]` / `PrepareToInstall` (`/q /norestart`), handling exit codes **3010 / 1641**
  (restart required → tell the user, or set `NeedsRestart`).
- CI downloads the redistributable from Microsoft once per build (cache it); verify its hash.
- **Licensing:** the .NET Framework redistributable may be redistributed with applications.
  **Windows update packages (.msu)** are a different matter — do not bundle them in the
  *public* release unless redistribution is confirmed; instead detect and show the KB numbers
  and download links. Decide after T1 shows which KBs are really needed.
- If a restart is required after .NET, install the app after the restart (or ask to rerun).

### 9.5 Out of scope for Phase 1

Any new feature (everything in Phase 2). Phase 1 only makes the existing product work on
Windows 7 and fixes what testing reveals.

### 9.6 Exit criteria (Definition of Done) → unlocks Phase 2

- [ ] T1–T18 pass on **Win 7 SP1 x64** and **x86** (T16 = documented "not supported").
- [ ] A Win 7 PC with **no internet and no .NET 4.8** can be fully set up from the release
      downloads alone (offline setup, item 1.2).
- [ ] Network tab (T11–T12) tested on Windows 10/11 and Windows 7, results recorded.
- [ ] Inno Setup version pinned and confirmed (1.3); asset names clear (1.4).
- [ ] Windows 7 section in the user guide + PDF (1.6).
- [ ] Owner sign-off → bump to `v2.0.0` development (Phase 2).

### 9.7 Test results log

| Date | Version | OS / variant | Tests | Result | Notes / issues |
|---|---|---|---|---|---|
| _—_ | _—_ | _—_ | _—_ | _—_ | _(to be filled during Phase 1)_ |

---

## 10. Phase 2 — Version 2 backlog (`v2.0.0`)

**Starts only after §9.6 is complete.** Every item must respect §8 (Windows 7 / net48).
Ordered by priority. Release Phase 2 as `v2.0.0` (pre-release builds may be published
as `v2.0.0-beta.N` without `[release]` on the main line — see §11).

### 2.1 Network tab hardening — *priority: high*

**Problem.** The workgroup push is untested and has known weaknesses.
**Work:**
- **Prerequisite guidance + pre-check button:** in a workgroup, remote admin with a *local*
  account (other than the built-in Administrator) is filtered by UAC remote restrictions
  → "Access denied". Targets need
  `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\LocalAccountTokenFilterPolicy`
  = `1` (DWORD) — set during imaging or via GPO — and firewall rules for **File and Printer
  Sharing** and **Remote Scheduled Tasks Management**. A "Check" button tests ping, `admin$`
  access and remote Task Scheduler per host before deploying.
- **No passwords on command lines:** replace `net use` with `WNetAddConnection2` and
  `schtasks /s … /p` with the TaskScheduler library's remote connection
  (`new TaskService(host, user, domain, password)`).
- **Clean up** the remote `TaskDelegatorDeploy` task after it finishes.
- **Verify the install**, not just the trigger (e.g. read the remote uninstall key
  `DisplayVersion`, or the version marker file) and report *Installed vX.Y.Z*.
- **Parallelism** with a limit (e.g. 8 concurrent hosts), per-host timeout, cancel button,
  export results to CSV.
**Acceptance:** 20+ hosts deploy in one run with per-host verified status; no credential
appears in any process command line.

### 2.2 Export / Import delegations (+ silent apply) — *priority: high*

**Problem.** Mass deployment installs the program but **not** the delegations; today each PC
must be configured by hand — the main blocker for a 100-PC lab.
**Design:**
- **Export** (Existing delegations tab) → JSON: friendly name, app path, arguments, run-as
  mode, target **user name or group name** (not SID — SIDs differ per PC), optional
  hash (2.3), schedule (2.5).
- **Admin-account passwords are not exported** (DPAPI LocalMachine is machine-bound). Import
  either uses SYSTEM mode, asks for the password, or takes it from a protected parameter.
- **Import** in the UI and silently: `TaskDelegator.exe --import <file.json> [--replace]`,
  with exit codes and a log, so GPO scripts / workgroup push can run it right after install.
- Network tab: option "also apply this configuration file" for push and GPO script.
**Acceptance:** one exported file + one push configures N PCs with working delegations.

### 2.3 Executable hash pinning — *priority: high (security)*

**Problem.** If a delegated `.exe` is replaced, the new file runs with admin rights.
**Design:** store **SHA-256 + size** in the descriptor at creation; `SessionLauncher` verifies
before launching; on mismatch → refuse, log, and show "changed — re-approve" in the admin
console (needed when the real app updates). Optional per delegation.
**Acceptance:** replacing the exe blocks the launch and is visible in logs/UI.

### 2.4 Group-based delegation — *priority: medium*

Delegate to a local/domain **group** (e.g. "Students") instead of single users: group SID in
the task SDDL (`(A;;GRGX;;;<groupSid>)`), launcher menu filtered by the user's group
membership, group picker in the UI.

### 2.5 Time-limited access — *priority: medium*

Allowed hours/days and/or an expiry date per delegation. **Enforced in `SessionLauncher`**
(runs as SYSTEM — authoritative), shown in the student launcher ("available 08:00–14:00").

### 2.6 Dangerous-application warning — *priority: medium (security)*

Warn (do not block) when delegating programs that hand out a general admin shell:
`cmd`, `powershell`/`pwsh`, `explorer`, `regedit`, `mmc`, `taskmgr`, `wscript`/`cscript`,
`mshta`, `rundll32`, and similar. Document in `SECURITY.md`.

### 2.7 Automated tests in CI — *priority: medium (quality)*

Unit-test project; run `dotnet test` in CI. Start with pure logic: `NormalizeExePath`
(move to a testable static class), `ExpandMachines`, `GpoStartupScript`, `BuildTsql`,
`SafeTaskName`, `IsGuiExecutable` (with sample PE bytes), JSON import/export round-trip.

### 2.8 Persian UI (RTL) with a language switch — *priority: medium*

`.resx` resources EN/FA, `RightToLeft` + `RightToLeftLayout` for FA, remembered choice;
**Tahoma** for Persian on Win 7 (§8). Re-check absolute layouts in RTL.

### 2.9 Update check — *priority: low*

Compare `AppInfo.Version` with the latest GitHub release; notify with a link. TLS 1.2 on
net48/Win 7 (§8); silent failure when offline; can be disabled.

### 2.10 Reports tab — *priority: low*

Read `audit.log` / `launcher.log`: who launched what and when, filters, CSV export.

### 2.11 Code signing — *priority: low (trust)*

Sign the exe and installer (e.g. SignPath Foundation, free for open source) to avoid
SmartScreen warnings. Note: SHA-256 signatures need the Win 7 SHA-2 updates (§9.3).

---

## 11. Versioning and branching policy

- Format `vMAJOR.MINOR.PATCH` in `VERSION`.
- **Phase 1:** stay on **`1.x.x`** — e.g. `1.15.0` for the offline/prerequisite installer,
  `1.15.x` for Win 7 fixes.
- **Phase 2:** **`v2.0.0`** — only after the owner signs off Phase 1 (§9.6).
- A release is published **only** by a commit containing `[release]`; **never** put
  `[release]` on work-in-progress commits.
- Each cloud session works on its **own branch** (assigned by the session) and pushes only
  there. Releases are created from the commit that carries `[release]`.
- If Phase 1 fixes and Phase 2 work ever overlap, merge Phase 1 fixes into the Phase 2
  branch regularly (shared files: `MainForm.cs`, `TaskDelegator.iss`, `VERSION`, docs).

---

## 12. Working conventions

- **Communication with the owner** is in Persian; code, comments and commit messages in English.
- **Docs are bilingual:** keep `docs/USER-GUIDE.md` and `docs/user-guide.html` in sync
  (EN + FA). The HTML becomes the release PDF — validate it before pushing.
- **Commit messages** describe what and why; end with the attribution lines required by
  the session. **No pull request unless the owner asks for one.**
- **Credit and licence:** keep "Created by Kamran Najafi" + GitHub/repository links in both
  windows and in the installer (licence requirement).
- **Before every push:** both targets must compile (CI checks), HTML well-formed, no
  `[release]` unless intended. **After a `[release]` push:** confirm CI success and 4 assets.
- **Testing:** the owner tests on real Windows. Ask for screenshots, `launcher.log` and
  `audit.log` (Fixes tab → "Open log & data folder").

---

## 13. Open questions

1. **Offline setup naming and scope** (item 1.2): final file name; whether any Windows
   update packages may be bundled or only linked.
2. **Restart handling** after .NET 4.8 installs on Win 7: install the app after reboot,
   or ask the user to rerun the setup?
3. **Default branch:** releases are tagged from session-branch commits. Should finished
   work also be merged into the repository's default branch?
4. **Domain environments:** does the lab use Active Directory? It affects 2.1 (GPO is the
   preferred path in a domain) and 2.4 (domain groups).

---

## خلاصهٔ فارسی — Persian summary

> **اول این را بخوانید.** این سند مرجعِ اصلیِ «پروژه کجاست، قدمِ بعدی چیست و چه قوانینی
> باید رعایت شود» است؛ طوری نوشته شده که یک توسعه‌دهندهٔ جدید — یا یک سشنِ جدیدِ Claude
> بدونِ تاریخچهٔ گفتگو — بتواند کار را درست ادامه دهد.
>
> **شروعِ سشنِ جدید:** «فایلِ `docs/ROADMAP.md` (و `HOW-IT-WORKS.md` و `SECURITY.md`) را
> بخوان و فاز N را ادامه بده.»

### وضعیتِ فعلی (نسخهٔ v1.14.0 — ۱۴ اکتبر ۲۰۲۶)

- **کار می‌کند و روی ویندوز ۱۰/۱۱ تست شده:** کنسولِ ادمین، لانچرِ دانشجو (بدونِ پنجرهٔ
  مشکی)، نمایشِ ورژن، اعتبارِ سازنده و لایسنس، تبِ **Fixes** (رفعِ خطای ۲۶۲ با تأیید)،
  حذفِ کامل و آپدیتِ تمیز، آیکن.
- **ساخته شده ولی هنوز تست نشده:** تبِ **Network**.
- **اصلاً تست نشده:** همه‌چیز روی **ویندوز ۷**.
- **کمبودهای شناخته‌شده:** ستاپ وجودِ **.NET Framework 4.8** را چک نمی‌کند؛ تبِ Network
  فقط **برنامه** را نصب می‌کند و **تفویض‌ها** را نه.

### دو فاز — به‌ترتیب

1. **فاز ۱ — تکمیلِ ویندوز ۷** (نسخه‌ها `1.x.x` می‌مانند).
2. **فاز ۲ — نسخهٔ ۲** (`v2.0.0`) — **فقط بعد از کامل‌شدنِ فاز ۱** شروع می‌شود.

### فاز ۱ — ویندوز ۷

**محیطِ تست:** ماشینِ مجازی ویندوز ۷ SP1 هم **۶۴ بیتی** و هم **۳۲ بیتی** (آزمایشگاه‌های قدیمی
معمولاً ۳۲ بیتی‌اند). از نصبِ تمیز **snapshot** بگیرید و قبل از هر تست برگردید. دو حالت:
**A** = بدونِ آپدیت و بدونِ .NET 4.8 (بدترین حالت)، **B** = با .NET 4.8.

**چک‌لیستِ تست (خلاصه):** نصب در حالت A و B، باز شدنِ کنسولِ ادمین و چیدمانِ درستِ ۴ تب،
ساختِ تفویض در هر دو حالتِ SYSTEM و حسابِ ادمین، اجرا توسطِ دانشجو (بدونِ پنجرهٔ مشکی)،
محدودیت‌های دانشجو، حذفِ تفویض، تبِ Fixes با SQL، تبِ Network (دستور، اسکریپت‌ها و push بینِ
ویندوز ۷ و ۱۰)، آپدیتِ تمیز (تفویض باید بماند)، حذفِ کامل، نسخهٔ zip، اینکه فایلِ net8 روی
ویندوز ۷ اجرا **نمی‌شود**، اسکریپتِ PowerShell روی نسخهٔ 2.0، و نمایشِ درستِ فارسی در PDF.
برای هر مورد: نتیجه + اسکرین‌شات + فایل‌های `launcher.log` و `audit.log`.
(جدولِ کاملِ T1 تا T18 در بخشِ 9.2 است.) تبِ Network را روی ویندوز ۱۰/۱۱ هم تست کنید.

**مشکلاتِ احتمالی:** نبودِ .NET 4.8؛ احتمالِ نیاز به آپدیت‌های ویندوز برای نصبِ خودِ .NET 4.8
(مثلاً پشتیبانیِ SHA-2: KB4474419 و KB4490628، و KB4019990 — باید در تست تأیید شود)؛
اینکه آخرین نسخهٔ Inno Setup هنوز روی ویندوز ۷ اجرا شود؛ نسخه‌های جدیدِ SQL/SSMS روی
ویندوز ۷ نصب نمی‌شوند.

**پیشنهاد برای پیش‌نیازها (تصمیمِ D11 — بعد از اولین تست قطعی می‌شود):**
یک اسکریپتِ ستاپ و **دو خروجی**:
- **`TaskDelegatorSetup.exe`** (حدود ۳ مگ) — برای ویندوز ۱۰/۱۱؛ اگر .NET 4.8 نبود، پیامِ
  واضح می‌دهد و لینکِ ستاپِ آفلاین را نشان می‌دهد.
- **ستاپِ آفلاین** (حدود ۱۲۰ مگ، مثلاً `TaskDelegatorSetup-Offline.exe`) — شاملِ .NET 4.8
  به‌صورتِ آفلاین؛ **فقط اگر نصب نباشد** نصبش می‌کند و بعد برنامه را. روی ۱۰/۱۱ هم کار می‌کند.
- تشخیص بر اساسِ **وجودِ .NET 4.8** است (کلیدِ رجیستری `Release ≥ 528040`)، نه اسمِ
  سیستم‌عامل — این‌طوری ویندوز ۸٫۱ و سرورهای بی‌آپدیت هم پوشش داده می‌شوند.
- بستهٔ .NET 4.8 قابلِ بازتوزیع است؛ ولی آپدیت‌های ویندوز (.msu) را در ریلیزِ عمومی قرار
  نمی‌دهیم مگر اجازهٔ بازتوزیع روشن شود — به‌جایش شمارهٔ KB و لینکِ دانلود را نشان می‌دهیم.

**کارهای فاز ۱:** اجرای تست‌ها (1.1)، پیش‌نیازها در ستاپ (1.2)، ثابت‌کردنِ نسخهٔ Inno
(1.3)، نام‌گذاریِ واضحِ فایل‌ها تا کاربرِ ویندوز ۷ فایلِ net8 را برندارد (1.4)، رفعِ باگ‌های
ویندوز ۷ (1.5)، بخشِ «ویندوز ۷» در راهنما و PDF (1.6).

**شرطِ پایانِ فاز ۱ (ورود به نسخهٔ ۲):** همهٔ تست‌ها روی ویندوز ۷ هر دو معماری قبول شوند؛
یک سیستمِ ویندوز ۷ **بدونِ اینترنت و بدونِ .NET** فقط با فایل‌های ریلیز کامل راه بیفتد؛
تبِ Network تست و ثبت شود؛ نسخهٔ Inno ثابت شود؛ راهنمای ویندوز ۷ آماده باشد؛ و **تأییدِ
شما**.

### فاز ۲ — نسخهٔ ۲ (به‌ترتیبِ اولویت)

1. **مقاوم‌سازیِ تبِ Network:** راهنما و دکمهٔ «بررسیِ پیش‌نیاز» (`LocalAccountTokenFilterPolicy`،
   فایروال)، حذفِ رمز از خطِ فرمان، پاک‌کردنِ تسکِ موقت، **تأییدِ نصب**، اجرای هم‌زمان.
2. **Export/Import تفویض‌ها** + دستورِ بی‌صدای `--import` — تا با یک فایل و یک push، ۱۰۰
   سیستم کامل تنظیم شوند (رمزهای حسابِ ادمین قابلِ export نیستند چون به همان سیستم قفل‌اند).
3. **قفلِ هشِ فایلِ برنامه (SHA-256)** — جلوگیری از جایگزین‌کردنِ exe.
4. **تفویض به گروه** (مثلاً گروهِ Students).
5. **دسترسیِ زمان‌دار** (ساعتِ کلاس / تاریخِ انقضا).
6. **هشدار برای برنامه‌های خطرناک** (cmd، powershell، explorer، regedit و…).
7. **تستِ خودکار** در CI.
8. **رابطِ فارسی (راست‌به‌چپ)** با انتخابِ زبان (فونتِ Tahoma برای ویندوز ۷).
9. **بررسیِ خودکارِ آپدیت.**
10. **تبِ گزارش‌ها** (چه کسی، چه برنامه‌ای، کی).
11. **امضای دیجیتال** فایل‌ها.

### قوانینِ همیشگی (هر دو فاز)

- نسخهٔ **net48** باید روی **ویندوز ۷ SP1 (۳۲ و ۶۴ بیتی)** کار کند: فقط از امکاناتی استفاده
  شود که در .NET Framework 4.8 و ویندوز ۷ هست؛ اسکریپت‌ها با PowerShell 2.0 سازگار باشند؛
  هر ارتباطِ اینترنتی با TLS 1.2 و بدونِ خطا در حالتِ آفلاین.
- چیدمانِ تب‌ها با **مختصاتِ ثابت** (نه AutoSize + Dock).
- راهنما همیشه **دوزبانه** و نسخهٔ md و html با هم به‌روز شوند (html → PDF).
- ریلیز فقط با کامیتِ دارای **`[release]`** و ورژنِ جدید در فایلِ `VERSION`؛ بعد از هر ریلیز،
  سبز بودنِ CI و وجودِ ۴ فایل تأیید شود.
- **نسخه‌ها:** فاز ۱ ← `1.x.x` (مثلاً `1.15.0` برای ستاپِ آفلاین)؛ فاز ۲ ← `v2.0.0` فقط بعد از
  تأییدِ شما.
- نامِ سازنده و لینک‌های گیت‌هاب در برنامه و ستاپ باید بماند (شرطِ لایسنس).
- محیطِ ابری ویندوز ندارد: کامپایل در CI انجام می‌شود و تستِ واقعی با شماست؛ برای عیب‌یابی
  لاگ و اسکرین‌شات لازم است.
