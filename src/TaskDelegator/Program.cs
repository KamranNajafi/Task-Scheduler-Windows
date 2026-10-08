using System.Diagnostics;
using System.Security.Principal;
using TaskDelegator.Services;
using TaskDelegator.Ui;

namespace TaskDelegator;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // 1) SYSTEM launcher path: invoked by the scheduled task as
        //    TaskDelegator.exe --launch "<taskName>"
        //    Runs as SYSTEM and spawns the approved app into the user's session.
        if (args.Length >= 2 && args[0].Equals("--launch", StringComparison.OrdinalIgnoreCase))
            return SessionLauncher.Run(args[1]);

#if NET8_0_OR_GREATER
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
#endif
        // On .NET Framework (Windows 7 build) DPI awareness comes from app.manifest.
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 2) Admin console: TaskDelegator.exe --admin  (self-elevates = the "admin login").
        if (args.Length >= 1 && args[0].Equals("--admin", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsElevated())
            {
                if (RelaunchElevated()) return 0;   // the elevated instance takes over
                MessageBox.Show("Administrator rights are required to configure delegations.",
                    "Task Delegator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }
            try { DelegationManager.EnsureSetup(); } catch { /* best effort */ }
            AuditLog.Write("LAUNCHED (elevated) admin console");
            Application.Run(new MainForm());
            return 0;
        }

        // 3) Default: run-only launcher for the standard user (no elevation).
        Application.Run(new StudentLauncherForm());
        return 0;
    }

    private static bool IsElevated()
    {
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private static bool RelaunchElevated()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                Arguments = "--admin",
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;   // UAC cancelled or denied
        }
    }
}
