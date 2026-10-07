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
on its own.

1. **Admin gate.** The GUI is marked `requireAdministrator`, so launching it raises
   a UAC prompt. Only an administrator can get past it and configure a delegation.
   *That prompt is the "admin login."*
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

## Get the executable

**CI/CD (GitHub Actions) builds the portable `.exe` automatically** — see
[`.github/workflows/build.yml`](.github/workflows/build.yml).

- **Every push / PR:** download `TaskDelegator-win-x64` from the run's **Artifacts**
  (Actions tab → latest run).
- **Tagged release (`v*`):** the `.exe` is attached to the GitHub **Release**.

### Build it yourself

Requires the .NET 8 SDK on a Windows machine:

```powershell
dotnet publish src/TaskDelegator/TaskDelegator.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -o publish
# -> publish\TaskDelegator.exe   (portable, self-contained)
```

---

## Usage

1. Copy `TaskDelegator.exe` onto the Windows machine (anywhere — it's portable).
2. Run it and approve the **UAC elevation** prompt as an administrator.
3. On the **Create delegation** tab:
   - Pick an application (or **Browse…** to a `.exe`).
   - Pick the **target Windows user** (works for standard users).
   - Leave **Run as: SYSTEM** (no password), or choose **Admin account** and enter
     an administrator account + password.
   - Leave **Create a shortcut on the user's desktop** checked.
   - Click **Create delegation**.
4. That user can now launch the app with admin rights from their desktop — even
   when logged in as a standard user, with the window on their own screen.
5. The **Existing delegations** tab lists everything and lets you remove any.

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

طرز استفاده:

1. `TaskDelegator.exe` را روی ویندوز اجرا کنید و در پنجرهٔ **UAC** به‌عنوان ادمین
   تأیید کنید (همین «لاگین ادمین» است).
2. در تب **Create delegation**: نرم‌افزار و کاربر را انتخاب کنید؛ حالت **SYSTEM**
   (بدون رمز) یا **Admin account** را انتخاب کنید؛ گزینهٔ ساخت شورتکات را فعال
   بگذارید و روی **Create delegation** بزنید.
3. حالا آن کاربر عادی می‌تواند از روی دسکتاپ خودش نرم‌افزار را با دسترسی ادمین و بدون
   رمز اجرا کند.

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

## Responsible use

Delegating an application is equivalent to granting `sudo` on it. **Only delegate
applications you trust**, and make sure the app's executable lives in a location the
user **cannot** write to (e.g. `Program Files`). Any app that can spawn arbitrary
child processes — a browser, Office, `cmd`, PowerShell, a file manager, anything with
a "Run…"/"Open with…" feature — effectively hands the user broader elevation. Choose
narrowly and review the audit log periodically.

## License

[MIT](LICENSE)
