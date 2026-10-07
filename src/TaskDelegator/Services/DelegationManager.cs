using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using TaskDelegator.Models;

namespace TaskDelegator.Services;

public sealed class CreateResult
{
    public string TaskPath { get; init; } = "";
    public string? ShortcutPath { get; init; }
    public string Note { get; init; } = "";
}

/// <summary>Creates, lists, and removes delegations (scheduled task + descriptor + shortcut).</summary>
public static class DelegationManager
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // -------- setup / hardening --------

    public static void EnsureSetup()
    {
        Paths.EnsureDirectories();
        // DataDir: Users may read/execute (needed for the hidden .vbs launcher & exe).
        Harden(Paths.DataDir, allowUsersRead: true);
        // Descriptors and credentials: SYSTEM + Administrators only. Standard users
        // must NOT be able to read (credential blobs) or tamper (descriptors drive
        // what runs as SYSTEM).
        Harden(Paths.DelegationsDir, allowUsersRead: false);
        Harden(Paths.CredentialsDir, allowUsersRead: false);
    }

    private static void Harden(string dir, bool allowUsersRead)
    {
        var args = $"\"{dir}\" /inheritance:r /grant:r *S-1-5-18:(OI)(CI)F *S-1-5-32-544:(OI)(CI)F";
        if (allowUsersRead) args += " *S-1-5-32-545:(OI)(CI)RX";
        RunIcacls(args);
    }

    private static void RunIcacls(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "icacls.exe"),
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(15000);
        }
        catch (Exception ex) { AuditLog.Write("icacls failed: " + ex.Message); }
    }

    // -------- create --------

    public static CreateResult Create(
        string exe, string? args, string friendlyName, LocalUserInfo user,
        RunAsMode mode, string? accountUser, string? accountPassword, bool makeShortcut)
    {
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            throw new ArgumentException("A valid executable (.exe) is required.");
        if (user is null || string.IsNullOrWhiteSpace(user.Sid))
            throw new ArgumentException("A target user is required.");
        if (string.IsNullOrWhiteSpace(friendlyName))
            friendlyName = Path.GetFileNameWithoutExtension(exe);
        if (mode == RunAsMode.Account && (string.IsNullOrWhiteSpace(accountUser) || string.IsNullOrWhiteSpace(accountPassword)))
            throw new ArgumentException("Account mode needs an administrator account and password.");

        EnsureSetup();
        string stableExe = EnsureInstalledExe();
        string taskName = SafeTaskName(friendlyName + "_" + user.Name);

        var cfg = new DelegationConfig
        {
            TaskName = taskName,
            FriendlyName = friendlyName,
            AppPath = exe,
            Arguments = string.IsNullOrWhiteSpace(args) ? null : args,
            WorkingDirectory = Path.GetDirectoryName(exe),
            Mode = mode,
            TargetUser = user.Name,
            TargetUserSid = user.Sid,
            CreatedBy = $"{Environment.UserDomainName}\\{Environment.UserName}",
            CreatedAtUtc = DateTime.UtcNow.ToString("o")
        };

        if (mode == RunAsMode.Account)
        {
            string credFile = Path.Combine(Paths.CredentialsDir, $"cred_{taskName}.bin");
            CredentialStore.Save(credFile, accountPassword!);
            cfg.AccountUser = accountUser;
            cfg.CredentialFile = credFile;
        }

        // Write the descriptor into the admin-only folder.
        File.WriteAllText(Path.Combine(Paths.DelegationsDir, taskName + ".json"),
            JsonSerializer.Serialize(cfg, JsonOpts), new UTF8Encoding(false));

        RegisterTask(taskName, stableExe, user.Sid, cfg.FriendlyName, user.Name);

        AuditLog.Write($"CREATED task={taskName} app='{exe}' args='{args}' mode={mode} runas='{(mode == RunAsMode.Account ? accountUser : "SYSTEM")}' foruser={user.Name} by={cfg.CreatedBy}");

        string? shortcut = null;
        string note = "";
        if (makeShortcut)
        {
            string? desktop = GetUserDesktop(user.Sid);
            if (string.IsNullOrEmpty(desktop))
            {
                desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
                note = " (user profile not found; shortcut placed on the Public desktop)";
            }
            shortcut = CreateShortcut(taskName, cfg.FriendlyName, exe, desktop);
            AuditLog.Write($"SHORTCUT {shortcut}");
        }

        return new CreateResult
        {
            TaskPath = $"\\{Paths.TaskFolder}\\{taskName}",
            ShortcutPath = shortcut,
            Note = note
        };
    }

    private static void RegisterTask(string taskName, string stableExe, string userSid, string friendly, string targetUser)
    {
        using var ts = new TaskService();
        var td = ts.NewTask();
        td.RegistrationInfo.Description = $"Delegated elevated launch of '{friendly}' for {targetUser}.";
        td.RegistrationInfo.Author = "TaskDelegator";

        td.Principal.UserId = "SYSTEM";
        td.Principal.LogonType = TaskLogonType.ServiceAccount;
        td.Principal.RunLevel = TaskRunLevel.Highest;

        td.Settings.Enabled = true;
        td.Settings.AllowDemandStart = true;
        td.Settings.Hidden = false;
        td.Settings.DisallowStartIfOnBatteries = false;
        td.Settings.StopIfGoingOnBatteries = false;
        td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
        td.Settings.MultipleInstances = TaskInstancesPolicy.Parallel;
        td.Settings.StartWhenAvailable = true;

        // The action runs OUR launcher as SYSTEM; it spawns the real app into the user's session.
        td.Actions.Add(new ExecAction(stableExe, $"--launch \"{taskName}\"", null));

        TaskFolder folder;
        try { folder = ts.GetFolder("\\" + Paths.TaskFolder); }
        catch { folder = ts.RootFolder.CreateFolder(Paths.TaskFolder); }

        // SDDL: Administrators + SYSTEM full; the delegated user gets read + execute
        // (enough to RUN this one task, nothing more).
        string sddl = $"O:BAG:BAD:(A;;GA;;;BA)(A;;GA;;;SY)(A;;GRGX;;;{userSid})";

        folder.RegisterTaskDefinition(taskName, td, TaskCreation.CreateOrUpdate,
            "SYSTEM", null, TaskLogonType.ServiceAccount, sddl);
    }

    // -------- list / remove --------

    public static List<DelegationRow> List()
    {
        var rows = new List<DelegationRow>();
        using var ts = new TaskService();
        TaskFolder folder;
        try { folder = ts.GetFolder("\\" + Paths.TaskFolder); }
        catch { return rows; }

        foreach (var t in folder.Tasks)
        {
            var row = new DelegationRow
            {
                TaskName = t.Name,
                LastRun = t.LastRunTime.Year < 2000 ? "never" : t.LastRunTime.ToString("yyyy-MM-dd HH:mm")
            };

            var cfgPath = Path.Combine(Paths.DelegationsDir, t.Name + ".json");
            if (File.Exists(cfgPath))
            {
                try
                {
                    var c = JsonSerializer.Deserialize<DelegationConfig>(File.ReadAllText(cfgPath));
                    if (c is not null)
                    {
                        row.FriendlyName = c.FriendlyName;
                        row.App = c.AppPath;
                        row.RunAs = c.Mode == RunAsMode.Account ? (c.AccountUser ?? "account") : "SYSTEM";
                        row.TargetUser = c.TargetUser;
                    }
                }
                catch { /* ignore unreadable descriptor */ }
            }
            if (string.IsNullOrEmpty(row.App) && t.Definition.Actions.FirstOrDefault() is ExecAction ea)
                row.App = ea.Path;

            rows.Add(row);
        }
        return rows;
    }

    public static void Remove(string taskName)
    {
        using (var ts = new TaskService())
        {
            try
            {
                var folder = ts.GetFolder("\\" + Paths.TaskFolder);
                folder.DeleteTask(taskName, false);
            }
            catch { /* already gone */ }
        }

        TryDelete(Path.Combine(Paths.DelegationsDir, taskName + ".json"));
        TryDelete(Path.Combine(Paths.CredentialsDir, $"cred_{taskName}.bin"));
        TryDelete(Path.Combine(Paths.DataDir, $"run_{taskName}.vbs"));
        RemoveShortcuts(taskName);

        AuditLog.Write($"REMOVED task={taskName}");
    }

    // -------- helpers --------

    public static bool IsTrustedLocation(string exe)
    {
        string[] roots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows)
        };
        return roots.Any(r => !string.IsNullOrEmpty(r) &&
                              exe.StartsWith(r, StringComparison.OrdinalIgnoreCase));
    }

    private static string EnsureInstalledExe()
    {
        string current = Environment.ProcessPath ?? Application.ExecutablePath;
        string target = Paths.InstalledExe;
        if (!string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
            File.Copy(current, target, overwrite: true);
        return target;
    }

    private static string SafeTaskName(string text)
    {
        var clean = Regex.Replace(text, @"[^\w\-]", "_");
        if (string.IsNullOrWhiteSpace(clean)) clean = "App";
        if (clean.Length > 120) clean = clean.Substring(0, 120);
        return Paths.TaskPrefix + "_" + clean;
    }

    private static string? GetUserDesktop(string sid)
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\" + sid);
            if (k?.GetValue("ProfileImagePath") is not string profile || !Directory.Exists(profile))
                return null;

            var oneDrive = Path.Combine(profile, "OneDrive", "Desktop");
            if (Directory.Exists(oneDrive)) return oneDrive;
            return Path.Combine(profile, "Desktop");
        }
        catch { return null; }
    }

    private static string CreateShortcut(string taskName, string friendly, string appExe, string desktop)
    {
        Directory.CreateDirectory(desktop);

        // Hidden launcher so no console window flashes when the user double-clicks.
        string vbs = Path.Combine(Paths.DataDir, $"run_{taskName}.vbs");
        string taskFull = $"\\{Paths.TaskFolder}\\{taskName}";
        string vbsLine = "CreateObject(\"WScript.Shell\").Run \"schtasks /run /tn \"\"" + taskFull + "\"\"\", 0, False";
        File.WriteAllText(vbs, vbsLine, new UTF8Encoding(false));

        string safe = Regex.Replace(friendly, @"[\\/:*?""<>|]", "_");
        string lnk = Path.Combine(desktop, safe + ".lnk");

        var t = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell is unavailable.");
        dynamic shell = Activator.CreateInstance(t)!;
        dynamic sc = shell.CreateShortcut(lnk);
        sc.TargetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wscript.exe");
        sc.Arguments = $"//B //Nologo \"{vbs}\"";
        if (File.Exists(appExe)) sc.IconLocation = appExe + ",0";
        sc.Description = $"Launch {friendly} (elevated, delegated by administrator)";
        sc.WindowStyle = 7;
        sc.Save();

        return lnk;
    }

    private static void RemoveShortcuts(string taskName)
    {
        var desktops = new List<string>();
        try
        {
            string usersRoot = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))?.FullName
                               ?? @"C:\Users";
            foreach (var profile in Directory.EnumerateDirectories(usersRoot))
            {
                desktops.Add(Path.Combine(profile, "Desktop"));
                desktops.Add(Path.Combine(profile, "OneDrive", "Desktop"));
            }
        }
        catch { /* ignore */ }
        desktops.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));

        foreach (var d in desktops)
        {
            if (!Directory.Exists(d)) continue;
            foreach (var lnk in SafeEnumerateLnks(d))
            {
                try
                {
                    var t = Type.GetTypeFromProgID("WScript.Shell");
                    if (t is null) return;
                    dynamic shell = Activator.CreateInstance(t)!;
                    dynamic sc = shell.CreateShortcut(lnk);
                    string argsVal = (string)sc.Arguments;
                    if (argsVal.Contains(taskName, StringComparison.OrdinalIgnoreCase))
                        TryDelete(lnk);
                }
                catch { /* ignore */ }
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateLnks(string dir)
    {
        try { return Directory.EnumerateFiles(dir, "*.lnk", SearchOption.TopDirectoryOnly).ToList(); }
        catch { return Array.Empty<string>(); }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }
}
