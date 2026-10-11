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

### 6. Errors & causes
| Error / symptom | Cause | Fix |
|---|---|---|
| "side-by-side configuration is incorrect" — `CreateProcess failed; code 14001` | An embedded Win32 manifest this Windows couldn't turn into an activation context (or a damaged component store / WinSxS). | Use this installer — it ships **without** that manifest. If it persists, run `sfc /scannow` then `DISM /Online /Cleanup-Image /RestoreHealth` and restart. |
| Portable `TaskDelegator.exe` (net8) won't start / missing `vcruntime140.dll` | The self-contained .NET 8 build needs the **Visual C++ Redistributable**, absent on clean machines. | Use the **installer** or the **net48** build (no VC++), or install VC++ 2015–2022 x64. |
| "This application requires .NET Framework 4.8" | .NET Framework 4.8 not installed (only on Windows 7). | Install .NET Framework 4.8 (in-box on Windows 10/11). |
| "Administrator rights are required" / UAC cancelled | The admin console needs elevation and the prompt was declined, or the account is not admin. | Run **Task Delegator (Administrator)** and approve UAC as an administrator. |
| Student clicks the shortcut but nothing opens | The delegated app moved/was removed, no interactive session, or the task couldn't start. | Check `C:\ProgramData\AppDelegation\launcher.log`; ensure the app exists and the user is signed in. |
| "No executable auto-detected for …" | The app didn't register a clean executable path. | Click **Browse…** and pick the real `.exe`. |
| Shortcut landed on the **Public** desktop | That user has never signed in (no profile yet). | Have them sign in once, or leave it on Public. |
| Warning: "executable is not under Program Files / Windows" | The delegated `.exe` is user-writable — it could be swapped for other code and run elevated. | Move the app to `Program Files` (admin-only) before delegating. |

### 7. Messages the program shows (and what they mean)

This lists **every** message the program can display: the **red** text in the bottom status
bar, and the **pop-up dialog boxes**. Red text means the action stopped and needs a fix.

**7.1 Administrator console — red text in the bottom status bar**

| Message | Cause | Fix |
|---|---|---|
| "Choose a valid executable (.exe)." | The **Executable** box is empty, or its path has surrounding quotes (`"C:\…\app.exe"`), a trailing icon index (`,0`), or an unexpanded `%variable%`, or the file isn't on this PC — this is why a path copied from the registry / a shortcut's *Target* is rejected even though the file exists. | Pick the app from the list or click **Browse…** to select the real `.exe`. Surrounding quotes, a `,0` icon index and `%environment%` variables are now cleaned automatically. |
| "No executable auto-detected for '…'." | The selected app's registry entry has no usable executable path (no `DisplayIcon` / `InstallLocation`). | Click **Browse…** and pick the program's real `.exe`. |
| "Select a target Windows user." | No user is selected in the **Target Windows user** list. | Click a user row (e.g. the Standard student account) before **Create delegation**. |
| "Account mode needs an administrator account and password." | **Run as: Admin account** is selected, but the Account or Password field is empty. | Fill both fields, or switch to **SYSTEM (no password)**. |
| "Select a delegation to remove." | **Remove selected** was clicked on the **Existing delegations** tab with no row selected. | Select a delegation row first, then **Remove selected**. |
| "Creating delegation…" | Informational (not red): the delegation is being created. | Wait a moment; a success or error message follows. |
| "Load error: …" / "List error: …" | The console couldn't read installed software, users, or existing tasks — usually it isn't elevated, or registry / Task Scheduler access was denied. | Run **Task Delegator (Administrator)** and approve UAC as an administrator. |
| "Failed: …" / "Remove failed: …" | Creating or removing the scheduled task failed — not elevated, the **Task Scheduler** service is stopped, or (Admin-account mode) a wrong account/password. The exact reason is appended (e.g. "Could not register the scheduled task…"). | Run elevated, ensure the **Task Scheduler** service is running, re-check the account/password, then retry. Details are in `C:\ProgramData\AppDelegation\audit.log`. |

**7.2 Administrator console — pop-up dialog boxes**

| Dialog (title: "Privileged App Delegation…") | When it appears | What to do |
|---|---|---|
| ⚠️ **Warning** — "The selected executable is not under Program Files or Windows… Continue anyway?" (**Yes** / **No**) | The chosen `.exe` sits in a folder a standard user can write to, so they could swap it and run their own code elevated. | Prefer **No**, move the app into `Program Files` (admin-only), and re-select it. Choose **Yes** only if you fully trust the location. |
| ✔️ **Information** — "Delegation created. User '…' can now launch '…' with administrator rights…" | A delegation was created successfully. | Nothing to fix — this confirms success. The program appears on the user's desktop and in their **Allowed Programs** launcher. |
| ❌ **Error** — the exact failure text (e.g. "Could not register the scheduled task…"; older builds showed "Object reference not set to an instance of an object") | Creating the delegation failed. "Object reference…" was a bug creating the *first* delegation (the Task Scheduler folder didn't exist yet) — fixed in this version. | Update to this version. Otherwise: run as administrator, ensure the **Task Scheduler** service is running, re-check any account/password, and retry. |
| ❓ **Question** — "Remove delegation '…' and its desktop shortcut(s)?" (**Yes** / **No**) | You clicked **Remove selected** on the **Existing delegations** tab. | **Yes** deletes the task and its shortcut(s); **No** cancels. |
| ⚠️ **Warning** — "Administrator rights are required to configure delegations." | The admin console was launched but the UAC prompt was declined, or the account is not an administrator. | Launch **Task Delegator (Administrator)** again and approve UAC with an administrator account. |

**7.3 "Allowed Programs" launcher (student) — bottom status bar**

| Message | Cause | Fix |
|---|---|---|
| "No programs have been assigned to you yet." | Informational: no delegation has been created for this user. | Not an error — an administrator assigns programs from the admin console. |
| "Select a program first." | **Run** was clicked with no program selected. | Click a program tile first, then **Run** (or just double-click the tile). |
| "Could not load programs: …" | The list of assigned programs couldn't be read (the menu folder is unreadable). | Ask an administrator to re-create the delegation / re-run setup; check `audit.log`. |
| "Could not start '…': …" | The delegated task couldn't be triggered — the task or app was removed, or there's no interactive session. | Make sure you're signed in and the app still exists; an administrator can read `C:\ProgramData\AppDelegation\launcher.log`. |
| "Administrator mode was cancelled (administrator rights required)." | The student clicked **Administrator…** but declined the UAC prompt, or is not an administrator. | Only administrators can open the console; approve UAC with an administrator account. |

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

### ۶. خطاها و علت‌ها
| خطا / نشانه | علت | راه‌حل |
|---|---|---|
| «side-by-side configuration is incorrect» — `CreateProcess failed; code 14001` | مانیفستِ Win32 داخل exe که این ویندوز نتوانست از آن activation context بسازد (یا خرابیِ Component Store / WinSxS). | از همین نصب‌کننده استفاده کنید — **بدون** آن مانیفست ساخته شده. اگر باز هم بود، `sfc /scannow` سپس `DISM /Online /Cleanup-Image /RestoreHealth` را اجرا و ری‌استارت کنید. |
| نسخهٔ پورتابل `TaskDelegator.exe` (net8) اجرا نمی‌شود / نبودِ `vcruntime140.dll` | نسخهٔ self-contained دات‌نت ۸ به **Visual C++ Redistributable** نیاز دارد که روی سیستم تازه‌نصب نیست. | از **نصب‌کننده** یا نسخهٔ **net48** استفاده کنید (بدون VC++)، یا VC++ 2015–2022 x64 را نصب کنید. |
| «This application requires .NET Framework 4.8» | دات‌نت فریم‌ورک ۴.۸ نصب نیست (فقط روی ویندوز ۷). | .NET Framework 4.8 را نصب کنید (روی ویندوز ۱۰/۱۱ توکار است). |
| «Administrator rights are required» / لغو UAC | کنسول ادمین به elevation نیاز دارد و پنجره رد شده، یا حساب ادمین نیست. | **Task Delegator (Administrator)** را اجرا و UAC را با حساب ادمین تأیید کنید. |
| دانشجو روی شورتکات کلیک می‌کند ولی چیزی باز نمی‌شود | برنامهٔ تفویض‌شده جابه‌جا/حذف شده، سشن تعاملی نیست، یا تسک اجرا نشده. | فایل `C:\ProgramData\AppDelegation\launcher.log` را ببینید؛ مطمئن شوید برنامه هست و کاربر لاگین کرده. |
| «No executable auto-detected for …» | آن نرم‌افزار مسیر اجراییِ تمیزی ثبت نکرده است. | روی **Browse…** بزنید و فایل `.exe` واقعی را انتخاب کنید. |
| شورتکات روی دسکتاپ **Public** ساخته شد | آن کاربر هرگز لاگین نکرده (هنوز پروفایل ندارد). | یک‌بار لاگین کند، یا روی Public بماند. |
| هشدار: «executable is not under Program Files / Windows» | فایل `.exe` تفویض‌شده قابل‌نوشتن توسط کاربر است — می‌تواند جایگزین و با دسترسی ادمین اجرا شود. | قبل از تفویض، برنامه را در `Program Files` (فقط-ادمین) بگذارید. |

### ۷. پیام‌هایی که برنامه نشان می‌دهد (و معنی‌شان)

این بخش **همهٔ** پیام‌هایی را که برنامه ممکن است نشان دهد فهرست می‌کند: متنِ **قرمز** در نوار
پایین، و **پنجره‌های pop-up**. متنِ قرمز یعنی کار متوقف شده و باید رفع شود.

**۷٫۱ کنسول ادمین — متنِ قرمز در نوار پایین**

| پیام | علت | راه‌حل |
|---|---|---|
| «Choose a valid executable (.exe).» | کادر **Executable** خالی است، یا مسیرِ آن کوتیشن دارد (`"C:\…\app.exe"`)، یا شمارهٔ آیکن در انتها دارد (`,0`)، یا `%variable%` بازنشده دارد، یا فایل روی این سیستم نیست — به همین دلیل مسیری که از رجیستری یا از *Target* یک شورتکات کپی شده باشد رد می‌شود، هرچند فایل وجود دارد. | برنامه را از لیست انتخاب کنید یا روی **Browse…** بزنید و فایل `.exe` واقعی را انتخاب کنید. کوتیشن‌های دورِ مسیر، شمارهٔ آیکن `,0` و متغیرهای `%environment%` حالا به‌طور خودکار پاک می‌شوند. |
| «No executable auto-detected for '…'.» | ورودیِ رجیستریِ آن نرم‌افزار مسیر اجراییِ قابل‌استفاده‌ای ندارد (نه `DisplayIcon` و نه `InstallLocation`). | روی **Browse…** بزنید و فایل `.exe` واقعیِ برنامه را انتخاب کنید. |
| «Select a target Windows user.» | هیچ کاربری در لیست **Target Windows user** انتخاب نشده است. | قبل از **Create delegation** روی یک ردیفِ کاربر کلیک کنید (مثلاً حساب Standard دانشجو). |
| «Account mode needs an administrator account and password.» | حالت **Run as: Admin account** انتخاب شده ولی کادر Account یا Password خالی است. | هر دو کادر را پر کنید، یا به حالت **SYSTEM (no password)** سوییچ کنید. |
| «Select a delegation to remove.» | در تب **Existing delegations** روی **Remove selected** زده شده بدون انتخابِ ردیف. | اول یک ردیفِ تفویض را انتخاب و سپس **Remove selected** را بزنید. |
| «Creating delegation…» | اطلاع‌رسانی (قرمز نیست): تفویض در حال ساخته‌شدن است. | چند لحظه صبر کنید؛ پیام موفقیت یا خطا پس از آن می‌آید. |
| «Load error: …» / «List error: …» | کنسول نتوانست نرم‌افزارهای نصب‌شده، کاربران یا تسک‌های موجود را بخواند — معمولاً چون elevated نیست، یا دسترسی به رجیستری / Task Scheduler رد شده است. | **Task Delegator (Administrator)** را اجرا و UAC را با حساب ادمین تأیید کنید. |
| «Failed: …» / «Remove failed: …» | ساخت یا حذفِ تسکِ زمان‌بندی‌شده شکست خورد — elevated نبودن، متوقف‌بودنِ سرویس **Task Scheduler**، یا (حالت Admin account) حساب/رمزِ اشتباه. علت دقیق به پیام اضافه می‌شود (مثلاً «Could not register the scheduled task…»). | برنامه را elevated اجرا کنید، از اجرای سرویس **Task Scheduler** مطمئن شوید، حساب/رمز را بررسی و دوباره تلاش کنید. جزئیات در `C:\ProgramData\AppDelegation\audit.log` هست. |

**۷٫۲ کنسول ادمین — پنجره‌های pop-up**

| پنجره (عنوان: «Privileged App Delegation…») | چه زمانی می‌آید | چه کنیم |
|---|---|---|
| ⚠️ **Warning** — «The selected executable is not under Program Files or Windows… Continue anyway?» (**Yes** / **No**) | فایل `.exe` انتخابی در مسیری است که کاربر عادی اجازهٔ نوشتن دارد؛ می‌تواند آن را عوض و کدِ خودش را با دسترسی ادمین اجرا کند. | ترجیحاً **No** بزنید، برنامه را به `Program Files` (فقط-ادمین) منتقل و دوباره انتخاب کنید. فقط اگر کاملاً اعتماد دارید **Yes** بزنید. |
| ✔️ **Information** — «Delegation created. User '…' can now launch '…' with administrator rights…» | تفویض با موفقیت ساخته شد. | کاری لازم نیست — یعنی موفقیت. برنامه روی دسکتاپ کاربر و در لانچرِ **Allowed Programs** می‌آید. |
| ❌ **Error** — متنِ دقیقِ خطا (مثلاً «Could not register the scheduled task…»؛ در نسخه‌های قدیمی «Object reference not set to an instance of an object») | ساختِ تفویض شکست خورد. «Object reference…» یک باگ هنگام ساختِ *اولین* تفویض بود (پوشهٔ Task Scheduler هنوز نبود) — در این نسخه رفع شد. | به این نسخه به‌روزرسانی کنید. وگرنه: به‌عنوان ادمین اجرا کنید، از اجرای سرویس **Task Scheduler** مطمئن شوید، حساب/رمز را بررسی و دوباره تلاش کنید. |
| ❓ **Question** — «Remove delegation '…' and its desktop shortcut(s)?» (**Yes** / **No**) | روی **Remove selected** در تب **Existing delegations** زدید. | **Yes** تسک و شورتکات(ها) را حذف می‌کند؛ **No** لغو می‌کند. |
| ⚠️ **Warning** — «Administrator rights are required to configure delegations.» | کنسول ادمین باز شد ولی پنجرهٔ UAC رد شد، یا حساب ادمین نیست. | دوباره **Task Delegator (Administrator)** را اجرا و UAC را با حساب ادمین تأیید کنید. |

**۷٫۳ لانچرِ «Allowed Programs» (دانشجو) — نوار پایین**

| پیام | علت | راه‌حل |
|---|---|---|
| «No programs have been assigned to you yet.» | اطلاع‌رسانی: هنوز هیچ تفویضی برای این کاربر ساخته نشده است. | خطا نیست — ادمین از کنسول ادمین برنامه‌ها را تخصیص می‌دهد. |
| «Select a program first.» | روی **Run** زده شده بدون انتخابِ برنامه. | اول روی کاشیِ یک برنامه کلیک و سپس **Run** را بزنید (یا دابل‌کلیک کنید). |
| «Could not load programs: …» | فهرستِ برنامه‌های تخصیص‌یافته خوانده نشد (پوشهٔ منو قابل‌خواندن نیست). | از ادمین بخواهید تفویض را دوباره بسازد / setup را دوباره اجرا کند؛ `audit.log` را ببینید. |
| «Could not start '…': …» | تسکِ تفویض‌شده اجرا نشد — تسک یا برنامه حذف شده، یا سشنِ تعاملی وجود ندارد. | مطمئن شوید لاگین کرده‌اید و برنامه هنوز هست؛ ادمین می‌تواند `C:\ProgramData\AppDelegation\launcher.log` را بخواند. |
| «Administrator mode was cancelled (administrator rights required).» | دانشجو روی **Administrator…** زد ولی UAC را رد کرد، یا ادمین نیست. | فقط ادمین‌ها می‌توانند کنسول را باز کنند؛ UAC را با حساب ادمین تأیید کنید. |

---

### Author / سازنده

**Created by Kamran Najafi — ساختهٔ کامران نجفی**

- GitHub: https://github.com/KamranNajafi
- Repository / مخزن: https://github.com/KamranNajafi/Task-Scheduler-Windows

نام سازنده و این لینک‌ها داخل خودِ برنامه هم نمایش داده می‌شوند (هم در کنسول ادمین و هم در
لانچرِ **Allowed Programs**).
