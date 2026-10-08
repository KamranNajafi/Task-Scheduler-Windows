# Task Scheduler — Privileged App Delegation (Windows)

A portable Windows IT-administration tool that lets an **administrator** grant a
**standard (non-admin) user** the right to run **one specific, approved
application with administrator privileges** — *without* ever giving that user the
administrator password, and **working even when the logged-in user is a standard
(non-admin) account**.

It is the Windows equivalent of a Linux `sudo` rule with `NOPASSWD` for a single
command. It is built on the **sanctioned Windows Task Scheduler + session APIs** —
there is no UAC bypass, no exploit, and no vulnerability abuse.

The tool:

- **Lists installed software** (from the Windows uninstall registry).
- **Lists local Windows users** (showing admin vs. standard).
- Lets an admin **pick an application + a target user** and create a delegation.
- Makes that app **runnable with admin rights by the chosen user**, with no
  password — and the app's window **appears on that standard user's own desktop**.
- Drops a **shortcut on that user's desktop**.
- **Lists and removes** delegations; every action is logged (auditable).

Single, self-contained, **portable `.exe`** — no installation, no .NET required on
the target machine.

---

## How it works (the legitimate mechanism)

Elevation is **delegated by an administrator**; the tool does not elevate anything
on its own. The one exe has three modes:

- **default (no arguments)** → the run-only **student launcher** (a standard user
  can open it with no UAC prompt).
- **`--admin`** → the **administrator console** (self-elevates via UAC = the "admin
  login"; only an administrator can get past it and configure delegations).
- **`--launch "<task>"`** → the SYSTEM launcher (invoked only by the scheduled task).

1. **Admin gate.** Configuration lives in the admin console, which self-elevates;
   students cannot reach it without administrator credentials at the UAC prompt.
2. **SYSTEM task.** For each approved app, the tool registers an **on-demand**
   scheduled task under `\AppDelegation\`, running as **SYSTEM** with **highest
   privileges**. The task's only action is to run this same tool as a launcher.
3. **Scoped permission.** The task's security descriptor is set so the chosen
   standard user may **run that one task** — and nothing else.
4. **Interactive launch.** When triggered, the SYSTEM launcher finds the user's
   interactive session and starts the approved app **elevated, on the user's
   desktop** (via `CreateProcessAsUser`). This is why it works for standard users
   and why the window is visible to them.
5. **Desktop shortcut.** A shortcut on the user's desktop triggers the task (hidden
   launcher, no console flash).

By default the app runs as **SYSTEM** (full local privileges, no password anywhere).
Optionally you can choose **"Admin account"** mode to run it as a specific
administrator user instead (that password is stored encrypted with DPAPI and is
readable only by SYSTEM/Administrators).

See [`docs/HOW-IT-WORKS.md`](docs/HOW-IT-WORKS.md) and
[`docs/SECURITY.md`](docs/SECURITY.md) for detail and the trust model.

---

## Supported operating systems

The tool is **Windows-only** (built on Windows Task Scheduler + Windows session
APIs). Two builds are produced:

| Build | Runs on | Notes |
|---|---|---|
| **`TaskDelegator.exe`** (net8, x64) | **Windows 10 / 11** and recent Windows Server | Portable, self-contained single file — **no .NET install required**. |
| **`TaskDelegator-win7-net48.zip`** (net48, AnyCPU) | **Windows 7 SP1, 8.1, 10, 11** (32- or 64-bit) | Extract and run `TaskDelegator.exe`; requires **.NET Framework 4.8** (preinstalled on Win 10/11; installable on Win 7 SP1). |

> Windows 7 reached end of support in 2020 and no longer receives security
> updates; the Win 7 build is provided for legacy labs that still require it.
> Windows XP is **not** supported (no UAC, no Task Scheduler 2.0).

## Get the executable

**CI/CD (GitHub Actions) builds everything automatically** — see
[`.github/workflows/build.yml`](.github/workflows/build.yml). Three downloads are
produced:

- **`TaskDelegatorSetup.exe`** — recommended installer for **Windows 10/11**.
  Installs the app to Program Files, creates Start-menu/desktop shortcuts, and — if
  the Microsoft Visual C++ runtime is missing — installs it silently from the bundled
  copy. No prerequisites to set up by hand; ideal for lab deployment (also
  GPO-deployable with `/VERYSILENT`).
- **`TaskDelegator.exe`** — portable single file (Win 10/11); needs the VC++ runtime.
- **`TaskDelegator-win7-net48.zip`** — Win 7 SP1 / 8.1 / 10 / 11; needs .NET
  Framework 4.8 (in-box on Win 10/11), no VC++ runtime.

Get them from the latest run's **Artifacts** (Actions tab), or attached to the
GitHub **Release** on a `v*` tag.

### Build it yourself

Requires the .NET 8 SDK on a Windows machine:

```powershell
# Windows 10/11 — portable, self-contained single file
dotnet publish src/TaskDelegator/TaskDelegator.csproj -c Release -f net8.0-windows `
  -r win-x64 --self-contained true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -o publish/net8
# -> publish\net8\TaskDelegator.exe

# Windows 7 SP1+ — needs .NET Framework 4.8 on the target machine
dotnet publish src/TaskDelegator/TaskDelegator.csproj -c Release -f net48 -o publish/net48
# -> publish\net48\  (TaskDelegator.exe + dependency DLLs)
```

---

## Usage

### Administrator — set up delegations

1. Copy `TaskDelegator.exe` onto the Windows machine (anywhere — it's portable).
2. Run it, then click **Administrator…** (or run `TaskDelegator.exe --admin`) and
   approve the **UAC** prompt as an administrator.
3. On the **Create delegation** tab:
   - Pick an application (or **Browse…** to a `.exe`).
   - Pick the **target Windows user** (works for standard users).
   - Leave **Run as: SYSTEM** (no password), or choose **Admin account** and enter
     an administrator account + password.
   - Keep **Add the 'Allowed Programs' launcher to the user's desktop** checked (and
     optionally the direct per-app shortcut).
   - Click **Create delegation**.
4. The **Existing delegations** tab lists everything and lets you remove any.

### Student / standard user — run allowed programs

The student just opens **Allowed Programs** (the desktop launcher, or
`TaskDelegator.exe` with no arguments — no UAC prompt). It lists the programs
assigned to them; double-clicking one runs it with administrator rights, on their
own desktop. The launcher is **run-only**: there is no uninstall, remove, or
configuration action, and a student cannot reach the admin console without
administrator credentials. This suits a training lab / classroom (آموزشگاه), where
students must run software that needs admin rights but must not be able to change
or remove it.

Creating a delegation copies the portable exe once to
`C:\ProgramData\AppDelegation\TaskDelegator.exe` so the scheduled task keeps working
after you move or delete the copy you ran. Actions are logged to
`C:\ProgramData\AppDelegation\audit.log` (and `launcher.log`).

---

## راهنمای فارسی (خلاصه)

ابزاری پورتابل برای ویندوز که به **ادمین** اجازه می‌دهد اجازهٔ اجرای **یک نرم‌افزار
مشخص با دسترسی ادمین** را به یک **کاربر عادی (غیرادمین)** بدهد، بدون دادن رمز ادمین —
و **برای کاربر استاندارد هم کار می‌کند** و پنجرهٔ برنامه روی دسکتاپِ خودِ کاربر
نمایش داده می‌شود. این کار با مکانیزم رسمی **Task Scheduler + APIهای سشن ویندوز**
انجام می‌شود (نه دور زدن UAC).

خروجی نهایی یک فایل **`TaskDelegator.exe`** تک‌فایلی و پورتابل است که توسط **CI/CD
(GitHub Actions)** ساخته می‌شود؛ از تب Actions در بخش Artifacts قابل دانلود است.

این برنامه برای **محیط آموزشگاه** مناسب است: دانشجو (کاربر استاندارد) فقط می‌تواند
برنامه‌های مجاز را **اجرا** کند و **نمی‌تواند** آن‌ها را حذف/uninstall کند یا به بخش
تنظیمات ادمین دسترسی پیدا کند.

طرز استفاده (ادمین):

1. `TaskDelegator.exe` را اجرا کنید، روی **Administrator…** بزنید (یا
   `TaskDelegator.exe --admin`) و در پنجرهٔ **UAC** به‌عنوان ادمین تأیید کنید (همین
   «لاگین ادمین» است).
2. در تب **Create delegation**: نرم‌افزار و کاربر را انتخاب کنید؛ حالت **SYSTEM**
   (بدون رمز) یا **Admin account** را انتخاب کنید؛ گزینهٔ **Allowed Programs launcher**
   را فعال بگذارید و روی **Create delegation** بزنید.

طرز استفاده (دانشجو / کاربر عادی):

- آیکن **Allowed Programs** روی دسکتاپ را باز کند (یا `TaskDelegator.exe` بدون
  آرگومان — بدون UAC). برنامه‌های مجازِ او لیست می‌شود؛ با دابل‌کلیک، برنامه با دسترسی
  ادمین و روی دسکتاپ خودش اجرا می‌شود. این پنجره **فقط اجرا** است — هیچ گزینهٔ حذف/
  uninstall یا تنظیماتی ندارد.

⚠️ **هشدار امنیتی:** تفویض یک نرم‌افزار دقیقاً مثل دادن `sudo` برای آن است. فقط
نرم‌افزارهای **مورد اعتماد و تک‌منظوره** را تفویض کنید. همچنین فایل اجرایی برنامه باید
در مسیری باشد که کاربر عادی اجازهٔ نوشتن در آن را ندارد (مثل `Program Files`)، وگرنه
کاربر می‌تواند آن را با کد دلخواه جایگزین کند.

---

## Notes & troubleshooting

- **No executable auto-detected.** Use **Browse…** to point at the real `.exe`.
- **Shortcut didn't appear.** If the target user has never logged on, their profile
  may not exist yet; the shortcut then goes to the **Public** desktop.
- **Nothing happens when the user clicks the shortcut.** Check
  `C:\ProgramData\AppDelegation\launcher.log` — it records the session it targeted
  and any error. The app must exist at the recorded path.
- **SYSTEM vs Admin account.** SYSTEM mode needs no password and is simplest. If an
  app misbehaves under the SYSTEM profile, use **Admin account** mode.
- **"The application has failed to start because its side-by-side configuration is
  incorrect."** This is the self-contained **net8** `TaskDelegator.exe` missing the
  **Microsoft Visual C++ Redistributable (x64)** — common on clean / freshly-imaged
  lab PCs. Two fixes: install the VC++ 2015–2022 x64 Redistributable on the machine,
  **or** use the **`TaskDelegator-win7-net48.zip`** build instead — it uses the
  built-in .NET Framework 4.8 (present on Windows 10/11) and needs no extra runtime,
  so it is the recommended build for computer labs.

## Responsible use

Delegating an application is equivalent to granting `sudo` on it. **Only delegate
applications you trust**, and make sure the app's executable lives in a location the
user **cannot** write to (e.g. `Program Files`). Any app that can spawn arbitrary
child processes — a browser, Office, `cmd`, PowerShell, a file manager, anything with
a "Run…"/"Open with…" feature — effectively hands the user broader elevation. Choose
narrowly and review the audit log periodically.

## License

[MIT](LICENSE)
