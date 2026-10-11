using System.Reflection;

namespace TaskDelegator.Services;

/// <summary>
/// Product metadata shown in the UI (window titles, header, credits) and used by the
/// installer. The version comes from the assembly, which the build stamps from the
/// repo-root VERSION file — so bumping VERSION updates the number shown everywhere.
/// </summary>
public static class AppInfo
{
    public const string ProductName = "Task Delegator";
    public const string Author = "Kamran Najafi";
    public const string GitHubUrl = "https://github.com/KamranNajafi";
    public const string RepositoryUrl = "https://github.com/KamranNajafi/Task-Scheduler-Windows";

    /// <summary>Display version such as "v1.7.0" (empty only if it cannot be read).</summary>
    public static string Version { get; } = Resolve();

    private static string Resolve()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();

            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                int plus = info!.IndexOf('+');          // drop any "+<commit>" build metadata
                if (plus >= 0) info = info.Substring(0, plus);
                info = info.Trim();
                if (info.Length > 0)
                    return info.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? info : "v" + info;
            }

            var v = asm.GetName().Version;
            if (v is not null) return $"v{v.Major}.{v.Minor}.{v.Build}";
        }
        catch { /* fall through */ }
        return "";
    }
}
