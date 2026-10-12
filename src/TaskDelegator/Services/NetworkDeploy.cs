using System.Diagnostics;
using System.Linq;
using System.Text;

namespace TaskDelegator.Services;

/// <summary>
/// Mass-deployment helpers so an admin doesn't install on every PC by hand:
///  1) a silent-install command + a ready batch script (for PsExec / PDQ / USB / login script),
///  2) a Group Policy computer-startup script (install/update from a share when the version changes),
///  3) a workgroup "push": remotely install to machines that share the same local-admin account,
///     using the admin$ share + a one-shot SYSTEM scheduled task (all in-box: net use, schtasks).
/// </summary>
public static class NetworkDeploy
{
    public const string SilentArgs = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART";

    public static string SilentCommand(string installer)
    {
        installer = string.IsNullOrWhiteSpace(installer) ? "TaskDelegatorSetup.exe" : installer.Trim();
        return $"\"{installer}\" {SilentArgs}";
    }

    /// <summary>A simple deploy .bat — run as administrator; installer from a UNC path or the same folder.</summary>
    public static string DeployBatch(string installer)
    {
        if (string.IsNullOrWhiteSpace(installer)) installer = "%~dp0TaskDelegatorSetup.exe";
        installer = installer.Trim();
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("REM Task Delegator - silent install. Run as administrator.");
        sb.AppendLine($"\"{installer}\" {SilentArgs}");
        sb.AppendLine("echo Exit code: %ERRORLEVEL%");
        return sb.ToString();
    }

    /// <summary>
    /// A Group Policy *computer startup* script: installs/updates from a share only when the
    /// installed version differs. Add under Computer Configuration ▸ Policies ▸ Windows Settings
    /// ▸ Scripts ▸ Startup. Put the installer on a share every PC can read.
    /// </summary>
    public static string GpoStartupScript(string installerUnc, string version)
    {
        if (string.IsNullOrWhiteSpace(installerUnc)) installerUnc = "\\\\SERVER\\share\\TaskDelegatorSetup.exe";
        if (string.IsNullOrWhiteSpace(version)) version = "v1.0.0";
        installerUnc = installerUnc.Trim();
        version = version.Trim();
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("REM Task Delegator - GPO computer-startup deploy (installs/updates from a share).");
        sb.AppendLine($"set \"SRC={installerUnc}\"");
        sb.AppendLine($"set \"WANT={version}\"");
        sb.AppendLine("set \"MARKER=%ProgramData%\\AppDelegation\\installed-version.txt\"");
        sb.AppendLine("set \"HAVE=\"");
        sb.AppendLine("if exist \"%MARKER%\" set /p HAVE=<\"%MARKER%\"");
        sb.AppendLine("if \"%HAVE%\"==\"%WANT%\" exit /b 0");
        sb.AppendLine($"\"%SRC%\" {SilentArgs}");
        sb.AppendLine("if %ERRORLEVEL%==0 (");
        sb.AppendLine("  if not exist \"%ProgramData%\\AppDelegation\" mkdir \"%ProgramData%\\AppDelegation\"");
        sb.AppendLine("  >\"%MARKER%\" echo %WANT%");
        sb.AppendLine(")");
        sb.AppendLine("exit /b %ERRORLEVEL%");
        return sb.ToString();
    }

    /// <summary>Parse the machine list: one host/IP per line (or comma-separated); "a.b.c.10-40" expands a last-octet range.</summary>
    public static List<string> ExpandMachines(string raw)
    {
        var hosts = new List<string>();
        foreach (var token in (raw ?? "").Replace(",", "\n").Split('\n'))
        {
            var line = token.Trim();
            if (line.Length == 0) continue;

            int dash = line.LastIndexOf('-');
            if (dash > 0 && line.Count(c => c == '.') == 3)
            {
                string head = line.Substring(0, dash).Trim();
                string tail = line.Substring(dash + 1).Trim();
                int lastDot = head.LastIndexOf('.');
                if (lastDot > 0
                    && int.TryParse(head.Substring(lastDot + 1), out int start)
                    && int.TryParse(tail, out int end)
                    && start >= 0 && end >= start && end <= 255)
                {
                    string prefix = head.Substring(0, lastDot + 1);
                    for (int i = start; i <= end; i++) hosts.Add(prefix + i);
                    continue;
                }
            }
            hosts.Add(line);
        }
        return hosts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Remotely install <paramref name="localInstaller"/> on <paramref name="host"/> using the
    /// shared local-admin <paramref name="user"/>/<paramref name="password"/>. Returns a short
    /// status string. The install is triggered (runs as SYSTEM on the target) and finishes
    /// asynchronously there.
    /// </summary>
    public static string PushToHost(string host, string user, string password, string localInstaller)
    {
        host = (host ?? "").Trim();
        if (host.Length == 0) return "empty host";
        if (user.IndexOf('"') >= 0 || password.IndexOf('"') >= 0) return "user/password must not contain a double-quote";

        string share = $"\\\\{host}\\admin$";
        bool connected = false;
        try
        {
            var (rc1, o1) = Run("net", $"use \"{share}\" /user:\"{user}\" \"{password}\"");
            if (rc1 != 0) return "connect failed — " + Clean(o1);
            connected = true;

            string remoteDir = share + "\\Temp";
            string remoteFile = remoteDir + "\\TaskDelegatorSetup.exe";
            try { if (!Directory.Exists(remoteDir)) Directory.CreateDirectory(remoteDir); } catch { }
            try { File.Copy(localInstaller, remoteFile, true); }
            catch (Exception ex) { return "copy to " + remoteDir + " failed — " + ex.Message; }

            const string tn = "TaskDelegatorDeploy";
            // No spaces in the remote path, so no nested quoting is needed inside /tr.
            string tr = "C:\\Windows\\Temp\\TaskDelegatorSetup.exe " + SilentArgs;
            var (rc3, o3) = Run("schtasks",
                $"/s {host} /u \"{user}\" /p \"{password}\" /create /tn \"{tn}\" /tr \"{tr}\" /sc ONCE /st 00:00 /ru SYSTEM /rl HIGHEST /f");
            if (rc3 != 0) return "schtasks create failed — " + Clean(o3);

            var (rc4, o4) = Run("schtasks", $"/s {host} /u \"{user}\" /p \"{password}\" /run /tn \"{tn}\"");
            if (rc4 != 0) return "schtasks run failed — " + Clean(o4);

            return "install triggered OK";
        }
        catch (Exception ex) { return "error — " + ex.Message; }
        finally
        {
            if (connected) { try { Run("net", $"use \"{share}\" /delete /y"); } catch { /* ignore */ } }
        }
    }

    private static (int code, string output) Run(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p is null) return (-1, "could not start " + file);
            string o = p.StandardOutput.ReadToEnd();
            string e = p.StandardError.ReadToEnd();
            p.WaitForExit(60000);
            int c = p.HasExited ? p.ExitCode : -1;
            return (c, (e + " " + o).Trim());
        }
        catch (Exception ex) { return (-1, ex.Message); }
    }

    private static string Clean(string s)
    {
        s = (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 160 ? s.Substring(0, 160) + "…" : s;
    }
}
