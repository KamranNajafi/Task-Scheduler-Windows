# Task Scheduler — Privileged App Delegation (Windows)

A small Windows IT-administration tool that lets an **administrator** grant a
**standard (non-admin) user** the right to run **one specific, approved
application with administrator privileges** — *without* ever giving that user the
administrator password.

It is the Windows equivalent of a Linux `sudo` rule with `NOPASSWD` for a single
command: an admin explicitly delegates elevated execution of a trusted app. It is
built entirely on the **sanctioned Windows Task Scheduler mechanism** — there is
no UAC bypass, no exploit, and no vulnerability abuse.

The tool can:

- **List installed software** (from the Windows uninstall registry).
- **List local Windows users** (and show who is an admin vs. standard user).
- Let an admin **pick an application + a target user** and create a delegation.
- Make that app **runnable with admin rights by the chosen user**, with no password.
- Drop a **shortcut on that user's desktop** that launches the app.
- **List and remove** existing delegations (fully auditable).

---

## How it works (the legitimate mechanism)

This tool does **not** bypass UAC or elevate anything on its own. The elevation is
*delegated by an administrator* using the mechanism Microsoft provides for it:

1. **Admin gate.** The tool must run elevated and self-elevates via UAC on launch.
   Only someone who can satisfy that UAC prompt (an administrator who knows the
   password) can configure a delegation. *That prompt is the "admin login."*
2. **Scheduled task.** For each approved app, the tool registers an **on-demand**
   task under `\AppDelegation\`, set to **Run with highest privileges**, running
   as a **stored administrator identity**. The admin supplies those credentials
   once, at setup; Windows stores them securely (they are never shown to the user).
3. **Scoped permission.** The task's security descriptor is set so the chosen
   standard user may **run that one task** (read + execute) — and nothing more.
   They cannot read the stored credentials, edit the task, or run anything else
   elevated.
4. **Desktop shortcut.** A shortcut on the user's desktop invokes
   `schtasks /run` for that task (via a hidden launcher, so no console flashes).

When the user double-clicks the shortcut, Task Scheduler launches the approved app
elevated. The user never sees or needs the admin password.

See [`docs/HOW-IT-WORKS.md`](docs/HOW-IT-WORKS.md) for the technical detail and
[`docs/SECURITY.md`](docs/SECURITY.md) for the trust model and limitations.

---

## Requirements

- Windows 10 or Windows 11.
- Windows PowerShell 5.1 (built in) **or** PowerShell 7+.
- An administrator account to configure delegations.

No build step and no installation — it is a single PowerShell script with a GUI.

---

## Usage

1. Clone or download this repository onto the Windows machine.
2. Double-click **`launch/Run-TaskDelegator.cmd`** (or run
   `src/TaskDelegator.ps1` in PowerShell).
3. Approve the **UAC elevation** prompt as an administrator.
4. In the **Create delegation** tab:
   - Pick an application from the list (or **Browse…** to a `.exe`).
   - Pick the **target Windows user**.
   - Confirm the **Run as admin** account + **password** (defaults to the current
     admin; you can point it at a dedicated admin account instead).
   - Leave **Create a desktop shortcut** checked.
   - Click **Create delegation**.
5. The chosen user can now launch that app with admin rights from their desktop.
6. The **Existing delegations** tab lists everything created and lets you remove
   any delegation (which also deletes its shortcut).

Every create/remove is written to `C:\ProgramData\AppDelegation\audit.log`.

---

## راهنمای فارسی (خلاصه)

این ابزار به **ادمین** اجازه می‌دهد که اجازهٔ اجرای **یک نرم‌افزار مشخص با دسترسی
ادمین** را به یک **کاربر عادی (غیرادمین)** بدهد، بدون اینکه رمز ادمین را به او بدهد.
این کار با مکانیزم رسمی **Task Scheduler** ویندوز انجام می‌شود (نه دور زدن UAC و نه
اکسپلویت).

روش استفاده:

1. فایل **`launch/Run-TaskDelegator.cmd`** را اجرا کنید.
2. در پنجرهٔ **UAC** به‌عنوان ادمین تأیید کنید (همین مرحله «لاگین ادمین» است؛ فقط
   کسی که رمز ادمین را دارد می‌تواند تنظیمات را انجام دهد).
3. در تب **Create delegation**:
   - از لیست، **نرم‌افزار نصب‌شده** را انتخاب کنید (یا با **Browse…** فایل `.exe`
     را انتخاب کنید).
   - **کاربر ویندوزی** موردنظر را انتخاب کنید.
   - حساب **Run as admin** و **رمز آن** را وارد کنید (به‌صورت امن توسط ویندوز ذخیره
     می‌شود و هرگز به کاربر نشان داده نمی‌شود).
   - گزینهٔ ساخت **شورتکات روی دسکتاپ** را فعال بگذارید.
   - روی **Create delegation** کلیک کنید.
4. حالا آن کاربر می‌تواند از روی دسکتاپ خودش، آن نرم‌افزار را با دسترسی ادمین و بدون
   رمز اجرا کند.
5. در تب **Existing delegations** می‌توانید موارد ساخته‌شده را ببینید و حذف کنید.

⚠️ **هشدار امنیتی:** تفویض یک نرم‌افزار دقیقاً مثل دادن `sudo` برای آن نرم‌افزار است.
فقط نرم‌افزارهای **مورد اعتماد** را تفویض کنید. نرم‌افزاری که می‌تواند خط‌فرمان باز
کند، فایل دلخواه اجرا کند یا کد دلخواه بارگذاری کند (مثل مرورگر، آفیس، cmd،
PowerShell، فایل‌منیجر) عملاً دسترسی ادمین گسترده‌تری می‌دهد.

---

## Notes & troubleshooting

- **No executable auto-detected.** Some apps don't register a clean executable
  path. Use **Browse…** to point at the real `.exe`.
- **Shortcut didn't appear.** If the target user has never logged on, their profile
  (and desktop) may not exist yet; the tool then places the shortcut on the
  **Public** desktop and tells you so.
- **GUI visibility.** The delegated app runs under a stored administrator identity,
  so it is launched in that identity's security context. For most line-of-business
  apps the window appears for the user who triggered it. If you delegate a GUI app
  and its window does not appear on the standard user's interactive desktop, that is
  Windows session isolation — tell me and I can add a SYSTEM-helper mode that always
  launches into the interactive session. CLI/background apps are unaffected.
- **Audit / cleanup.** Everything is logged to
  `C:\ProgramData\AppDelegation\audit.log`, and the tasks live under the
  `\AppDelegation\` folder in Task Scheduler. Use the **Existing delegations** tab to
  remove any delegation.

## Responsible use

Delegating an application is equivalent to granting `sudo` on it. **Only delegate
applications you trust.** Any app that can spawn arbitrary child processes — a
browser, an Office app, `cmd`, PowerShell, a file manager, anything with a
"Run…"/"Open with…" feature — effectively hands the user broader elevation. Choose
narrowly, prefer single-purpose line-of-business apps, and review
`C:\ProgramData\AppDelegation\audit.log` periodically. This is the same caveat that
applies to `sudo` and to any privilege-delegation mechanism.

## License

[MIT](LICENSE)
