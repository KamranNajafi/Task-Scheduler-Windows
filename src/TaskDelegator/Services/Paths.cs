namespace TaskDelegator.Services;

/// <summary>Well-known paths and names used across the app.</summary>
public static class Paths
{
    public const string TaskFolder = "AppDelegation";
    public const string TaskPrefix = "Delegated";

    public static string DataDir =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AppDelegation");

    public static string DelegationsDir => System.IO.Path.Combine(DataDir, "delegations");
    public static string CredentialsDir => System.IO.Path.Combine(DataDir, "creds");
    // Student-readable menu entries (display name + task name only; no secrets).
    public static string MenuDir => System.IO.Path.Combine(DataDir, "menu");
    public static string AuditLog => System.IO.Path.Combine(DataDir, "audit.log");
    public static string LauncherLog => System.IO.Path.Combine(DataDir, "launcher.log");

    /// <summary>Stable copy of this exe used by scheduled tasks (survives moving the portable exe).</summary>
    public static string InstalledExe => System.IO.Path.Combine(DataDir, "TaskDelegator.exe");

    public static void EnsureDirectories()
    {
        System.IO.Directory.CreateDirectory(DataDir);
        System.IO.Directory.CreateDirectory(DelegationsDir);
        System.IO.Directory.CreateDirectory(CredentialsDir);
        System.IO.Directory.CreateDirectory(MenuDir);
    }
}
