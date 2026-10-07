# Security model, trust boundaries, and limitations

## What this tool is

A **privilege-delegation** tool for administrators — the Windows analogue of
granting `sudo <specific-command>` with `NOPASSWD`. An administrator decides, per
application, that a particular standard user may run a particular trusted program
elevated, and (unlike a plain stored-credential task) the elevated app is launched
into that user's interactive session so it actually works for non-admin users.

## What this tool is **not**

- **Not** a UAC bypass. No auto-elevating binary, COM, DLL-search-order, token-theft,
  or vulnerability is used. The SYSTEM context comes from a scheduled task an
  administrator registered, nothing more.
- **Not** a general grant of admin rights. The user gains the ability to run *one
  pre-approved task*, which starts *one pre-approved application*.
- **Not** a password discloser. In SYSTEM mode there is no password at all. In
  Account mode the admin supplies the password once; it is stored encrypted and is
  never shown to the user.

Every configuration step requires an administrator to pass UAC first.

## Trust boundaries

| Actor | Can | Cannot |
|---|---|---|
| Administrator (configures) | Create/remove delegations; choose app, user, mode | — |
| Standard user (delegated) | Run the one delegated app elevated | Read credentials; edit the task or descriptor; create delegations; run anything else elevated |

Enforced by: the task SDDL (user gets only read+execute on that task); and the
`delegations\` / `creds\` folder ACLs (SYSTEM + Administrators only — standard users
cannot read or modify them).

## Run-as modes

- **SYSTEM (default).** The app runs as `NT AUTHORITY\SYSTEM` (full local
  privileges) in the user's session. No password is stored anywhere. Note SYSTEM is
  a superset of Administrator for local resources and uses the SYSTEM profile; a few
  apps dislike running as SYSTEM.
- **Admin account.** The app runs as a specific administrator account. The password
  is protected with DPAPI (`LocalMachine` scope) and the blob is ACL'd to SYSTEM +
  Administrators only, so a standard user cannot read it (and therefore cannot
  decrypt it).

## The real risks (and how to avoid them)

1. **Choosing what to delegate** — the same risk as `sudo`. An app that can open a
   shell, run/"open with" an arbitrary file, or load arbitrary plugins/macros
   (browsers, Office, terminals, scripting hosts, file managers, installers) lets the
   user run arbitrary code elevated. **Delegate only narrow, single-purpose, trusted
   apps.**
2. **Writable executable path** — if the delegated `.exe` (or its folder) is writable
   by the user, they can replace it and have SYSTEM run their code. **Keep delegated
   apps in a location only administrators can write** (e.g. `Program Files`). The GUI
   warns when the target is outside `Program Files`/`Windows`.
3. **Fixed arguments** — prefer passing fixed arguments so the app always starts in
   the intended mode.

## Hardening suggestions

- Prefer **Admin account** mode with a dedicated least-privileged admin account over
  SYSTEM when the app supports it and you want a bounded identity.
- Review `C:\ProgramData\AppDelegation\audit.log` and `launcher.log`, and the
  `\AppDelegation\` task folder, periodically. Remove delegations no longer needed.
- Keep delegated apps patched — a vulnerable delegated app is an elevation path.

## Reporting

This is an administrative tool for machines/domains you administer. Do not use it to
obtain privileges you have not been granted.
