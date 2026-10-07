using System.Text.Json;
using TaskDelegator.Models;
using TaskDelegator.Native;

namespace TaskDelegator.Services;

/// <summary>
/// Runs as SYSTEM (invoked by the scheduled task with `--launch &lt;taskName&gt;`).
/// Reads the delegation descriptor and launches the approved app, elevated, into
/// the interactive user's session so a standard user can see and use it.
/// </summary>
public static class SessionLauncher
{
    public static int Run(string taskName)
    {
        try
        {
            var cfgPath = Path.Combine(Paths.DelegationsDir, taskName + ".json");
            if (!File.Exists(cfgPath))
            {
                AuditLog.Launcher($"launch '{taskName}': descriptor not found at {cfgPath}");
                return 2;
            }

            DelegationConfig? cfg;
            try { cfg = JsonSerializer.Deserialize<DelegationConfig>(File.ReadAllText(cfgPath)); }
            catch (Exception ex) { AuditLog.Launcher($"launch '{taskName}': bad descriptor: {ex.Message}"); return 2; }

            if (cfg is null || string.IsNullOrWhiteSpace(cfg.AppPath))
            {
                AuditLog.Launcher($"launch '{taskName}': empty descriptor");
                return 2;
            }
            if (!File.Exists(cfg.AppPath))
            {
                AuditLog.Launcher($"launch '{taskName}': app not found: {cfg.AppPath}");
                return 3;
            }

            uint session = NativeMethods.GetTargetSessionId();
            if (session == 0xFFFFFFFF)
            {
                AuditLog.Launcher($"launch '{taskName}': no active interactive session");
                return 4;
            }

            IntPtr token;
            string err;
            if (cfg.Mode == RunAsMode.Account && !string.IsNullOrWhiteSpace(cfg.AccountUser) && !string.IsNullOrWhiteSpace(cfg.CredentialFile))
            {
                SplitUser(cfg.AccountUser!, out string domain, out string user);
                string password;
                try { password = CredentialStore.Load(cfg.CredentialFile!); }
                catch (Exception ex) { AuditLog.Launcher($"launch '{taskName}': credential load failed: {ex.Message}"); return 5; }
                token = NativeMethods.CreateAccountPrimaryToken(user, domain, password, out err);
            }
            else
            {
                token = NativeMethods.CreateSystemPrimaryToken(out err);
            }

            if (token == IntPtr.Zero)
            {
                AuditLog.Launcher($"launch '{taskName}': token creation failed: {err}");
                return 5;
            }

            try
            {
                string workDir = cfg.WorkingDirectory
                    ?? Path.GetDirectoryName(cfg.AppPath)
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.System);

                bool ok = NativeMethods.LaunchInSession(session, token, cfg.AppPath, cfg.Arguments, workDir, out string lerr);
                AuditLog.Launcher($"launch '{taskName}': session={session} mode={cfg.Mode} app='{cfg.AppPath}' ok={ok} {lerr}");
                return ok ? 0 : 6;
            }
            finally { NativeMethods.SafeClose(token); }
        }
        catch (Exception ex)
        {
            AuditLog.Launcher($"launch '{taskName}': unhandled: {ex}");
            return 1;
        }
    }

    private static void SplitUser(string account, out string domain, out string user)
    {
        int i = account.IndexOf('\\');
        if (i > 0)
        {
            domain = account[..i];
            user = account[(i + 1)..];
        }
        else
        {
            domain = ".";
            user = account;
        }
    }
}
