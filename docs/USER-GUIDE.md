# Task Delegator — User Guide / راهنمای کاربر

> A printable **PDF** of this guide (English + Persian) is attached to every
> [GitHub Release](https://github.com/KamranNajafi/Task-Scheduler-Windows/releases)
> as `TaskDelegator-User-Guide.pdf`.
>
> نسخهٔ **PDF** این راهنما (انگلیسی + فارسی) در هر
> [ریلیز](https://github.com/KamranNajafi/Task-Scheduler-Windows/releases) با نام
> `TaskDelegator-User-Guide.pdf` پیوست شده است.

```
Administrator ──creates a delegation──▶ SYSTEM task (+ desktop shortcut) ──▶ Standard user runs the app as admin
```

---

## English

### 1. What it is
**Task Delegator** lets an administrator allow a **standard (non-admin) user** to run
specific, approved applications with **administrator rights** — without giving out the
administrator password. Ideal for a **training lab / classroom**: students can launch
software that needs admin rights, but cannot uninstall it or change anything.

### 2. Install
1. Run `TaskDelegatorSetup.exe` and approve the **UAC** prompt.
2. It installs to `Program Files` and adds shortcuts: **Allowed Programs** (for
   everyone) and **Task Delegator (Administrator)**.

> **No prerequisites** — the installer uses the .NET Framework 4.8 built into
> Windows 10/11.

### 3. Administrator — create a delegation
1. Open **Task Delegator (Administrator)** (or run the app and click **Administrator…**)
   and approve **UAC**. This is the "admin login".
2. On the **Create delegation** tab:
   - **Installed application** — pick it from the list (type to filter), or **Browse…**
     to its `.exe`.
   - **Target Windows user** — pick the user (works for Standard users).
   - **Run as** — **SYSTEM (no password)** is recommended; or choose **Admin account**
     and type an administrator account + password.
   - Keep the two shortcut checkboxes ticked.
3. Click **Create delegation**.
4. The **Existing delegations** tab lists everything and lets you remove any.

### 4. Student / standard user — run allowed programs
1. Open **Allowed Programs** from the desktop (no UAC prompt).
2. Double-click a program — it starts **with administrator rights, on your own desktop**.

The launcher is **run-only**: no uninstall, remove, or settings action.

### 5. Security notes
- Delegating an app is like granting `sudo` on it — **only delegate trusted,
  single-purpose apps**.
- Keep the app's `.exe` in a folder users cannot write to (e.g. `Program Files`).
- Every action is logged to `C:\ProgramData\AppDelegation\audit.log`.

### 6. Troubleshooting
| Problem | Fix |
|---|---|
| "side-by-side configuration is incorrect" (code 14001) | Use this installer (it ships without the manifest that caused it). On an old/corrupted Windows also run `sfc /scannow` and `DISM /Online /Cleanup-Image /RestoreHealth`, then restart. |
| Shortcut didn't appear | If the user has never signed in, it goes to the **Public** desktop. |
| Nothing happens on click | Check `C:\ProgramData\AppDelegation\launcher.log`. |

---

## فارسی

### ۱. این برنامه چیست؟
**Task Delegator** به **ادمین** اجازه می‌دهد اجازهٔ اجرای **یک نرم‌افزار مشخص با دسترسی
ادمین** را به یک **کاربر عادی (غیرادمین)** بدهد — **بدون دادن رمز ادمین**. مناسب برای
**محیط آموزشگاه / آزمایشگاه**: دانشجو می‌تواند نرم‌افزاری را که به دسترسی ادمین نیاز دارد
اجرا کند، ولی نمی‌تواند آن را حذف (uninstall) یا تنظیماتش را تغییر دهد.

### ۲. نصب
۱. فایل `TaskDelegatorSetup.exe` را اجرا کنید و پنجرهٔ **UAC** را تأیید کنید.
۲. برنامه در `Program Files` نصب می‌شود و شورتکات‌ها را می‌سازد: **Allowed Programs**
   (برای همه) و **Task Delegator (Administrator)**.

> **بدون پیش‌نیاز** — نصب‌کننده از .NET Framework 4.8 که در ویندوز ۱۰/۱۱ توکار است
> استفاده می‌کند.

### ۳. ادمین — ساخت یک تفویض (delegation)
۱. **Task Delegator (Administrator)** را باز کنید (یا برنامه را اجرا و روی
   **Administrator…** بزنید) و **UAC** را تأیید کنید. همین «لاگین ادمین» است.
۲. در تب **Create delegation**:
   - **نرم‌افزار نصب‌شده** را از لیست انتخاب کنید (برای فیلتر تایپ کنید) یا با **Browse…**
     فایل `.exe` را انتخاب کنید.
   - **کاربر ویندوز** موردنظر را انتخاب کنید (برای کاربر Standard هم کار می‌کند).
   - **Run as**: حالت **SYSTEM (بدون رمز)** پیشنهاد می‌شود؛ یا **Admin account** را انتخاب
     و حساب ادمین + رمز را وارد کنید.
   - هر دو گزینهٔ شورتکات را فعال بگذارید.
۳. روی **Create delegation** کلیک کنید.
۴. در تب **Existing delegations** می‌توانید همه را ببینید و حذف کنید.

### ۴. دانشجو / کاربر عادی — اجرای برنامه‌های مجاز
۱. آیکن **Allowed Programs** را از روی دسکتاپ باز کنید (بدون UAC).
۲. روی یک برنامه دابل‌کلیک کنید — **با دسترسی ادمین و روی دسکتاپ خودتان** اجرا می‌شود.

این پنجره **فقط-اجرا** است: هیچ گزینهٔ حذف/uninstall یا تنظیماتی ندارد.

### ۵. نکات امنیتی
- تفویض یک نرم‌افزار مثل دادن `sudo` روی آن است — **فقط نرم‌افزارهای مورد اعتماد و
  تک‌منظوره را تفویض کنید**.
- فایل `.exe` برنامه را در مسیری بگذارید که کاربر اجازهٔ نوشتن ندارد (مثل `Program Files`).
- همهٔ عملیات در `C:\ProgramData\AppDelegation\audit.log` ثبت می‌شود.

### ۶. رفع اشکال
| مشکل | راه‌حل |
|---|---|
| خطای «side-by-side configuration is incorrect» (کد ۱۴۰۰۱) | از همین نصب‌کننده استفاده کنید (بدون آن مانیفستِ مشکل‌دار ساخته شده). روی ویندوزِ قدیمی/خراب، `sfc /scannow` و `DISM /Online /Cleanup-Image /RestoreHealth` را اجرا و ری‌استارت کنید. |
| شورتکات ساخته نشد | اگر آن کاربر هرگز لاگین نکرده باشد، روی دسکتاپ **Public** گذاشته می‌شود. |
| با کلیک چیزی اجرا نمی‌شود | فایل `C:\ProgramData\AppDelegation\launcher.log` را بررسی کنید. |
