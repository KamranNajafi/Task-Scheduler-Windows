# Security model, trust boundaries, and limitations

## What this tool is

A **privilege-delegation** tool for administrators. It is the Windows analogue of
granting `sudo <specific-command>` with `NOPASSWD` on Linux. An administrator
decides, explicitly and per-application, that a particular standard user may run a
particular trusted program elevated.

## What this tool is **not**

- It is **not** a UAC bypass. It does not exploit auto-elevating binaries, COM
  interfaces, DLL search order, token theft, or any vulnerability.
- It does **not** grant the user administrator rights generally. The user gains the
  ability to *run one pre-approved task*, nothing else.
- It does **not** reveal the administrator password. Credentials are supplied by the
  admin at setup and stored by Windows; the user never sees them.

Everything it does requires an administrator to have authenticated via UAC first.

## Trust boundaries

| Actor | Can | Cannot |
|---|---|---|
| Administrator (configures) | Create/remove delegations; choose apps, users, run-as account | — |
| Standard user (delegated) | Run the specific delegated app elevated via the task/shortcut | Read stored credentials; edit the task; create new delegations; run other apps elevated |

The task's security descriptor grants the delegated user only **Generic Read +
Generic Execute** on that one task.

## The real risk: choosing what to delegate

This is the same risk `sudo` carries. **Delegating an app is as powerful as the app
is.** If a delegated application can:

- open a command prompt or PowerShell,
- run or "open with" an arbitrary executable,
- load arbitrary plugins/macros/scripts (browsers, Office, IDEs),
- write to locations that influence later elevated execution,

then the user can leverage it to run arbitrary code elevated — i.e. you have
effectively given them admin. **Delegate only narrow, single-purpose, trusted
applications.**

Good candidates: a legacy line-of-business app that needs admin to write to its own
protected folder; a specific hardware/diagnostic utility.

Poor candidates: browsers, Office apps, file managers, terminals, scripting hosts,
installers, anything with a built-in "run a program" feature.

## Hardening suggestions

- Prefer a **dedicated, least-privileged admin account** as the run-as identity
  rather than a Domain Admin.
- Pass fixed **arguments** where possible so the app always starts in the intended
  mode.
- Review `C:\ProgramData\AppDelegation\audit.log` and the `\AppDelegation\` task
  folder periodically; remove delegations that are no longer needed.
- Keep delegated apps patched — a vulnerable delegated app is an elevation path.

## Reporting

This is an administrative tool intended for machines/domains you administer. Do not
use it to obtain privileges you have not been granted.
