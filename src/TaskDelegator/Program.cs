using TaskDelegator.Services;
using TaskDelegator.Ui;

namespace TaskDelegator;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // SYSTEM launcher path: invoked by the scheduled task as
        //   TaskDelegator.exe --launch "<taskName>"
        // Runs as SYSTEM and spawns the approved app into the user's session.
        if (args.Length >= 2 && args[0].Equals("--launch", StringComparison.OrdinalIgnoreCase))
            return SessionLauncher.Run(args[1]);

        // Administration GUI path (the manifest forces elevation = the "admin login").
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try { DelegationManager.EnsureSetup(); } catch { /* best effort */ }
        AuditLog.Write("LAUNCHED (elevated) TaskDelegator GUI");

        Application.Run(new MainForm());
        return 0;
    }
}
