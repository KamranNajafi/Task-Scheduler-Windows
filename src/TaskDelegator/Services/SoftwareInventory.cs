using System.Text.RegularExpressions;
using Microsoft.Win32;
using TaskDelegator.Models;

namespace TaskDelegator.Services;

/// <summary>Enumerates installed applications from the uninstall registry keys.</summary>
public static class SoftwareInventory
{
    private static readonly Regex SkipName =
        new(@"^(KB\d+|Update for|Security Update|Hotfix)", RegexOptions.IgnoreCase);

    private static readonly Regex SkipExe =
        new(@"(unins|setup|install|update|crash|report|helper|service)", RegexOptions.IgnoreCase);

    public static List<InstalledApp> GetAll()
    {
        var map = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

        ReadRoot(RegistryHive.LocalMachine, RegistryView.Registry64, map);
        ReadRoot(RegistryHive.LocalMachine, RegistryView.Registry32, map);
        ReadRoot(RegistryHive.CurrentUser, RegistryView.Default, map);

        return map.Values.OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static void ReadRoot(RegistryHive hive, RegistryView view, Dictionary<string, InstalledApp> map)
    {
        const string sub = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(sub);
            if (uninstall is null) return;

            foreach (var name in uninstall.GetSubKeyNames())
            {
                try
                {
                    using var k = uninstall.OpenSubKey(name);
                    if (k is null) continue;

                    var display = k.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(display)) continue;
                    if (SkipName.IsMatch(display)) continue;
                    if (k.GetValue("SystemComponent") is int sc && sc == 1) continue;

                    var app = new InstalledApp
                    {
                        DisplayName = display.Trim(),
                        Version = k.GetValue("DisplayVersion") as string,
                        Publisher = k.GetValue("Publisher") as string,
                        InstallLocation = k.GetValue("InstallLocation") as string,
                    };
                    app.Executable = ResolveExecutable(k.GetValue("DisplayIcon") as string, app.InstallLocation);

                    map[app.DisplayName] = app; // de-dupe by display name
                }
                catch { /* skip unreadable entry */ }
            }
        }
        catch { /* skip unreadable hive/view */ }
    }

    private static string? ResolveExecutable(string? displayIcon, string? installLocation)
    {
        // 1) DisplayIcon usually points at the main exe: "C:\path\app.exe,0"
        if (!string.IsNullOrWhiteSpace(displayIcon))
        {
            var candidate = displayIcon.Split(',')[0].Trim().Trim('"');
            if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
                return candidate;
        }

        // 2) Otherwise pick a plausible main exe inside the install location.
        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
        {
            try
            {
                var best = new DirectoryInfo(installLocation)
                    .EnumerateFiles("*.exe", SearchOption.TopDirectoryOnly)
                    .Where(f => !SkipExe.IsMatch(f.Name))
                    .OrderByDescending(f => f.Length)
                    .FirstOrDefault();
                if (best is not null) return best.FullName;
            }
            catch { /* ignore */ }
        }

        return null;
    }
}
