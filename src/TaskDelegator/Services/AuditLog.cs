namespace TaskDelegator.Services;

/// <summary>Simple append-only audit/diagnostic logging.</summary>
public static class AuditLog
{
    private static readonly object Gate = new();

    public static void Write(string message) => Append(Paths.AuditLog, message);

    public static void Launcher(string message) => Append(Paths.LauncherLog, message);

    private static void Append(string file, string message)
    {
        try
        {
            Paths.EnsureDirectories();
            string who;
            try { who = $"{Environment.UserDomainName}\\{Environment.UserName}"; }
            catch { who = "?"; }
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {who}  {message}{Environment.NewLine}";
            lock (Gate)
            {
                System.IO.File.AppendAllText(file, line);
            }
        }
        catch
        {
            // Logging must never throw.
        }
    }
}
