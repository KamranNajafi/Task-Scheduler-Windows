using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using TaskDelegator.Models;

namespace TaskDelegator.Services;

/// <summary>
/// The run-only student side: lists the apps delegated to the current user and
/// triggers them. It only ever RUNS a pre-approved task — it cannot create,
/// modify, remove, or uninstall anything.
/// </summary>
public static class StudentMenu
{
    public static string CurrentUserSid()
    {
        try { return WindowsIdentity.GetCurrent().User?.Value ?? ""; }
        catch { return ""; }
    }

    public static List<MenuEntry> GetForCurrentUser()
    {
        var sid = CurrentUserSid();
        var list = new List<MenuEntry>();
        try
        {
            if (!Directory.Exists(Paths.MenuDir)) return list;
            foreach (var f in Directory.EnumerateFiles(Paths.MenuDir, "*.json"))
            {
                try
                {
                    var e = JsonSerializer.Deserialize<MenuEntry>(File.ReadAllText(f));
                    if (e is null || string.IsNullOrEmpty(e.TaskName)) continue;
                    if (string.IsNullOrEmpty(e.TargetUserSid) ||
                        string.Equals(e.TargetUserSid, sid, StringComparison.OrdinalIgnoreCase))
                        list.Add(e);
                }
                catch { /* skip bad entry */ }
            }
        }
        catch { /* menu dir unreadable */ }

        return list.OrderBy(e => e.FriendlyName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Trigger the delegated task (run-only). The user has run rights via the task
    /// SDDL; Task Scheduler then launches the approved app as SYSTEM into this
    /// session. This method cannot run anything that was not delegated.
    /// </summary>
    public static void Run(MenuEntry entry)
    {
        string taskPath = $"\\{Paths.TaskFolder}\\{entry.TaskName}";
        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe"),
            Arguments = $"/run /tn \"{taskPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var p = Process.Start(psi);
        p?.WaitForExit(15000);
    }
}
