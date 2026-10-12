using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace TaskDelegator.Services;

/// <summary>
/// One-click fix for the SQL Server "CREATE DATABASE permission denied in database
/// 'master'" error (Error 262) hit by a delegated SSMS/sqlcmd. A delegation runs the
/// app as NT AUTHORITY\SYSTEM, which since SQL Server 2012 is not a sysadmin by
/// default; this grants the chosen login a server role (dbcreator or sysadmin) on the
/// local instances, so it can create databases. Runs the grant via sqlcmd using the
/// current (elevated administrator) Windows identity, which must itself be a SQL
/// sysadmin — normally the account that installed SQL Server.
/// </summary>
public static class SqlServerFix
{
    public sealed class Result
    {
        public bool AnySuccess { get; set; }
        public bool AnyFailure { get; set; }
        public bool SqlcmdMissing { get; set; }
        public bool NoInstances { get; set; }
        public string Report { get; set; } = "";
        public string Tsql { get; set; } = "";
    }

    /// <summary>Local SQL Server instance monikers (".", ".\SQLEXPRESS", …) from the registry.</summary>
    public static List<string> GetLocalInstances()
    {
        var list = new List<string>();
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL");
            if (k is null) return list;
            foreach (var name in k.GetValueNames())
            {
                if (string.IsNullOrEmpty(name)) continue;
                list.Add(name.Equals("MSSQLSERVER", StringComparison.OrdinalIgnoreCase) ? "." : ".\\" + name);
            }
        }
        catch { /* none discoverable */ }
        return list;
    }

    // Windows account names / instance names never contain these; reject to keep the
    // T-SQL identifiers and string literal safe.
    private static bool IsUnsafe(string s) => s.IndexOfAny(new[] { '"', '\'', '[', ']', ';', '\r', '\n' }) >= 0;

    public static string BuildTsql(string login, string role) =>
        $"IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'{login}') " +
        $"CREATE LOGIN [{login}] FROM WINDOWS; " +
        $"ALTER SERVER ROLE [{role}] ADD MEMBER [{login}];";

    /// <summary>
    /// Grant <paramref name="login"/> the <paramref name="role"/> ("dbcreator" or
    /// "sysadmin") on <paramref name="instance"/> (null/empty = all local instances).
    /// </summary>
    public static Result ApplyGrant(string login, string role, string? instance)
    {
        login = (login ?? "").Trim();
        role = (role ?? "").Trim().ToLowerInvariant();
        if (login.Length == 0) login = "NT AUTHORITY\\SYSTEM";
        if (role != "dbcreator" && role != "sysadmin") role = "dbcreator";

        var r = new Result { Tsql = BuildTsql(login, role) };

        if (IsUnsafe(login) || (!string.IsNullOrWhiteSpace(instance) && IsUnsafe(instance!)))
        {
            r.AnyFailure = true;
            r.Report = "The account or instance name contains characters that are not allowed.";
            return r;
        }

        var instances = !string.IsNullOrWhiteSpace(instance)
            ? new List<string> { instance!.Trim() }
            : GetLocalInstances();

        if (instances.Count == 0)
        {
            r.NoInstances = true;
            r.Report = "No local SQL Server instance was found on this PC.\r\n" +
                       "If SQL Server is on another machine, run the T-SQL there (as a SQL administrator).";
            return r;
        }

        string? sqlcmd = FindSqlcmd();
        if (sqlcmd is null)
        {
            r.SqlcmdMissing = true;
            r.Report = "sqlcmd was not found on this PC, so the fix could not be applied automatically.\r\n" +
                       "Open SQL Server Management Studio (as a SQL administrator) and run the T-SQL below.";
            return r;
        }

        var sb = new StringBuilder();
        foreach (var inst in instances)
        {
            var (ok, output) = RunSqlcmd(sqlcmd, inst, r.Tsql);
            if (ok) { r.AnySuccess = true; sb.AppendLine($"[OK]   {inst}"); }
            else
            {
                r.AnyFailure = true;
                sb.AppendLine($"[FAIL] {inst} — {Shorten(output)}");
            }
        }

        if (r.AnySuccess && !r.AnyFailure)
            sb.AppendLine().AppendLine("Done. In SSMS, disconnect and reconnect (or restart it) so the new role takes effect, then create the database again.");
        else if (r.AnyFailure)
            sb.AppendLine().AppendLine("For any instance that failed, you must run this as a SQL administrator (normally the account that installed SQL Server).");

        r.Report = sb.ToString().TrimEnd();
        return r;
    }

    private static (bool ok, string output) RunSqlcmd(string sqlcmd, string instance, string tsql)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = sqlcmd,
                // -E = Windows auth as the current (elevated) user; -b = set errorlevel on error.
                Arguments = $"-S \"{instance}\" -E -b -Q \"{tsql}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p is null) return (false, "could not start sqlcmd");
            string so = p.StandardOutput.ReadToEnd();
            string se = p.StandardError.ReadToEnd();
            p.WaitForExit(30000);
            int code = p.HasExited ? p.ExitCode : -1;
            string msg = (se + " " + so).Trim();
            return (code == 0, msg.Length == 0 ? (code == 0 ? "ok" : "exit " + code) : msg);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private static string? FindSqlcmd()
    {
        // 1) PATH.
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                var candidate = Path.Combine(dir.Trim(), "sqlcmd.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* skip bad PATH entry */ }
        }
        // 2) Common install locations (SQL Server client tools).
        foreach (var root in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        })
        {
            if (string.IsNullOrEmpty(root)) continue;
            string baseDir = Path.Combine(root, "Microsoft SQL Server");
            if (!Directory.Exists(baseDir)) continue;
            try
            {
                foreach (var f in Directory.EnumerateFiles(baseDir, "sqlcmd.exe", SearchOption.AllDirectories))
                    return f;
            }
            catch { /* ignore */ }
        }
        return null;
    }

    private static string Shorten(string s)
    {
        s = (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 160 ? s.Substring(0, 160) + "…" : s;
    }
}
