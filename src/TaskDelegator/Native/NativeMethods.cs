using System.Runtime.InteropServices;
using System.Text;

namespace TaskDelegator.Native;

/// <summary>
/// P/Invoke declarations and the SYSTEM-context helpers used to launch an
/// elevated process into a standard user's interactive session.
/// </summary>
internal static class NativeMethods
{
    // ---- access rights / constants ----
    private const uint TOKEN_DUPLICATE = 0x0002;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
    private const uint TOKEN_ADJUST_DEFAULT = 0x0080;
    private const uint TOKEN_ADJUST_SESSIONID = 0x0100;
    private const uint MAXIMUM_ALLOWED = 0x02000000;

    private const int LOGON32_LOGON_INTERACTIVE = 2;
    private const int LOGON32_PROVIDER_DEFAULT = 0;

    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint CREATE_NEW_CONSOLE = 0x00000010;
    private const int STARTF_USESHOWWINDOW = 0x00000001;
    private const short SW_SHOW = 5;

    private const int WinBuiltinAdministratorsSid = 26;
    private const int MAX_PREFERRED_LENGTH = -1;
    private const uint INVALID_SESSION = 0xFFFFFFFF;

    private enum SECURITY_IMPERSONATION_LEVEL { Anonymous, Identification, Impersonation, Delegation }
    private enum TOKEN_TYPE { TokenPrimary = 1, TokenImpersonation = 2 }
    private enum TOKEN_INFORMATION_CLASS { TokenSessionId = 12, TokenLinkedToken = 19 }
    private enum WTS_INFO_CLASS { WTSUserName = 5 }
    private const int WTSActive = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WTS_SESSION_INFO
    {
        public int SessionId;
        public IntPtr pWinStationName;
        public int State;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LOCALGROUP_MEMBERS_INFO_0 { public IntPtr lgrmi0_sid; }

    // ---- imports ----
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] private static extern uint WTSGetActiveConsoleSessionId();
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr p);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr h, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(IntPtr existing, uint access, IntPtr attrs,
        SECURITY_IMPERSONATION_LEVEL level, TOKEN_TYPE type, out IntPtr newToken);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool SetTokenInformation(IntPtr token, TOKEN_INFORMATION_CLASS cls, ref uint info, uint len);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr token, TOKEN_INFORMATION_CLASS cls, IntPtr info, uint len, out uint ret);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LogonUser(string user, string? domain, string password, int logonType, int provider, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(IntPtr token, string? appName, StringBuilder? cmdLine,
        IntPtr procAttr, IntPtr threadAttr, bool inherit, uint flags, IntPtr env, string? curDir,
        ref STARTUPINFO si, out PROCESS_INFORMATION pi);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSEnumerateSessions(IntPtr server, int reserved, int version, out IntPtr info, out int count);

    [DllImport("wtsapi32.dll")] private static extern void WTSFreeMemory(IntPtr p);

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WTSQuerySessionInformation(IntPtr server, uint sessionId, WTS_INFO_CLASS cls, out IntPtr buffer, out uint bytes);

    [DllImport("userenv.dll", SetLastError = true)] private static extern bool CreateEnvironmentBlock(out IntPtr env, IntPtr token, bool inherit);
    [DllImport("userenv.dll", SetLastError = true)] private static extern bool DestroyEnvironmentBlock(IntPtr env);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetLocalGroupGetMembers(string? server, string group, int level, out IntPtr buf,
        int prefMax, out int read, out int total, ref IntPtr resume);

    [DllImport("netapi32.dll")] private static extern int NetApiBufferFree(IntPtr buf);

    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool CreateWellKnownSid(int type, IntPtr domainSid, IntPtr sid, ref uint cb);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool ConvertSidToStringSid(IntPtr sid, out IntPtr str);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupAccountSid(string? system, IntPtr sid, StringBuilder name, ref uint cchName,
        StringBuilder domain, ref uint cchDomain, out int use);

    // ---- public helpers ----

    public static void SafeClose(IntPtr h) { if (h != IntPtr.Zero) CloseHandle(h); }

    /// <summary>SIDs (as strings) of all members of the local Administrators group.</summary>
    public static HashSet<string> GetAdministratorMemberSids()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var group = GetAdministratorsGroupName();
        if (group is null) return set;

        IntPtr buf = IntPtr.Zero, resume = IntPtr.Zero;
        int status = NetLocalGroupGetMembers(null, group, 0, out buf, MAX_PREFERRED_LENGTH, out int read, out _, ref resume);
        if (status == 0 && buf != IntPtr.Zero)
        {
            try
            {
                int size = Marshal.SizeOf<LOCALGROUP_MEMBERS_INFO_0>();
                IntPtr p = buf;
                for (int i = 0; i < read; i++)
                {
                    var info = Marshal.PtrToStructure<LOCALGROUP_MEMBERS_INFO_0>(p);
                    if (info.lgrmi0_sid != IntPtr.Zero && ConvertSidToStringSid(info.lgrmi0_sid, out IntPtr s))
                    {
                        var str = Marshal.PtrToStringUni(s);
                        if (str is not null) set.Add(str);
                        LocalFree(s);
                    }
                    p = IntPtr.Add(p, size);
                }
            }
            finally { NetApiBufferFree(buf); }
        }
        return set;
    }

    private static string? GetAdministratorsGroupName()
    {
        uint cb = 0;
        CreateWellKnownSid(WinBuiltinAdministratorsSid, IntPtr.Zero, IntPtr.Zero, ref cb);
        if (cb == 0) return null;
        IntPtr sid = Marshal.AllocHGlobal((int)cb);
        try
        {
            if (!CreateWellKnownSid(WinBuiltinAdministratorsSid, IntPtr.Zero, sid, ref cb)) return null;
            var name = new StringBuilder(256); uint cchName = 256;
            var dom = new StringBuilder(256); uint cchDom = 256;
            if (!LookupAccountSid(null, sid, name, ref cchName, dom, ref cchDom, out _)) return null;
            return name.ToString();
        }
        finally { Marshal.FreeHGlobal(sid); }
    }

    /// <summary>Returns the session id of the interactive user, or 0xFFFFFFFF if none.</summary>
    public static uint GetTargetSessionId()
    {
        uint console = WTSGetActiveConsoleSessionId();
        uint chosen = INVALID_SESSION, firstActiveWithUser = INVALID_SESSION;

        if (WTSEnumerateSessions(IntPtr.Zero, 0, 1, out IntPtr info, out int count))
        {
            try
            {
                int size = Marshal.SizeOf<WTS_SESSION_INFO>();
                IntPtr p = info;
                for (int i = 0; i < count; i++)
                {
                    var si = Marshal.PtrToStructure<WTS_SESSION_INFO>(p);
                    p = IntPtr.Add(p, size);
                    if (si.State != WTSActive) continue;
                    bool hasUser = !string.IsNullOrEmpty(GetSessionUser((uint)si.SessionId));
                    if ((uint)si.SessionId == console && hasUser) { chosen = console; break; }
                    if (hasUser && firstActiveWithUser == INVALID_SESSION) firstActiveWithUser = (uint)si.SessionId;
                }
            }
            finally { WTSFreeMemory(info); }
        }

        if (chosen == INVALID_SESSION) chosen = firstActiveWithUser;
        if (chosen == INVALID_SESSION && console != INVALID_SESSION && console != 0) chosen = console;
        return chosen;
    }

    private static string GetSessionUser(uint session)
    {
        if (WTSQuerySessionInformation(IntPtr.Zero, session, WTS_INFO_CLASS.WTSUserName, out IntPtr buf, out _) && buf != IntPtr.Zero)
        {
            try { return Marshal.PtrToStringUni(buf) ?? ""; }
            finally { WTSFreeMemory(buf); }
        }
        return "";
    }

    /// <summary>Duplicate the current (SYSTEM) token into a primary token.</summary>
    public static IntPtr CreateSystemPrimaryToken(out string error)
    {
        error = "";
        if (!OpenProcessToken(GetCurrentProcess(),
                TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_ASSIGN_PRIMARY | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID,
                out IntPtr tok))
        {
            error = "OpenProcessToken failed (" + Marshal.GetLastWin32Error() + ")";
            return IntPtr.Zero;
        }
        try
        {
            if (!DuplicateTokenEx(tok, MAXIMUM_ALLOWED, IntPtr.Zero,
                    SECURITY_IMPERSONATION_LEVEL.Impersonation, TOKEN_TYPE.TokenPrimary, out IntPtr dup))
            {
                error = "DuplicateTokenEx failed (" + Marshal.GetLastWin32Error() + ")";
                return IntPtr.Zero;
            }
            return dup;
        }
        finally { CloseHandle(tok); }
    }

    /// <summary>Logon as an administrator account and return its elevated primary token.</summary>
    public static IntPtr CreateAccountPrimaryToken(string user, string? domain, string password, out string error)
    {
        error = "";
        if (!LogonUser(user, domain, password, LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT, out IntPtr hUser))
        {
            error = "LogonUser failed (" + Marshal.GetLastWin32Error() + ")";
            return IntPtr.Zero;
        }
        try
        {
            IntPtr linked = TryGetLinkedToken(hUser);
            IntPtr source = linked != IntPtr.Zero ? linked : hUser;
            bool ok = DuplicateTokenEx(source, MAXIMUM_ALLOWED, IntPtr.Zero,
                SECURITY_IMPERSONATION_LEVEL.Impersonation, TOKEN_TYPE.TokenPrimary, out IntPtr prim);
            if (linked != IntPtr.Zero) CloseHandle(linked);
            if (!ok) { error = "DuplicateTokenEx failed (" + Marshal.GetLastWin32Error() + ")"; return IntPtr.Zero; }
            return prim;
        }
        finally { CloseHandle(hUser); }
    }

    private static IntPtr TryGetLinkedToken(IntPtr token)
    {
        IntPtr buf = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            if (GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenLinkedToken, buf, (uint)IntPtr.Size, out _))
                return Marshal.ReadIntPtr(buf);
        }
        catch { /* ignore */ }
        finally { Marshal.FreeHGlobal(buf); }
        return IntPtr.Zero;
    }

    /// <summary>Launch the process in the given session using the given elevated primary token.</summary>
    public static bool LaunchInSession(uint sessionId, IntPtr primaryToken, string app, string? args, string? workDir, out string error)
    {
        error = "";
        uint sess = sessionId;
        if (!SetTokenInformation(primaryToken, TOKEN_INFORMATION_CLASS.TokenSessionId, ref sess, sizeof(uint)))
            error = "SetTokenInformation(session) warning (" + Marshal.GetLastWin32Error() + "); ";

        IntPtr env = IntPtr.Zero;
        CreateEnvironmentBlock(out env, primaryToken, false);

        var si = new STARTUPINFO
        {
            cb = Marshal.SizeOf<STARTUPINFO>(),
            lpDesktop = @"winsta0\default",
            dwFlags = STARTF_USESHOWWINDOW,
            wShowWindow = SW_SHOW
        };

        var cmd = new StringBuilder();
        cmd.Append('"').Append(app).Append('"');
        if (!string.IsNullOrWhiteSpace(args)) cmd.Append(' ').Append(args);

        bool ok = CreateProcessAsUser(primaryToken, app, cmd, IntPtr.Zero, IntPtr.Zero, false,
            CREATE_UNICODE_ENVIRONMENT | CREATE_NEW_CONSOLE, env, workDir, ref si, out PROCESS_INFORMATION pi);

        if (!ok) error += "CreateProcessAsUser failed (" + Marshal.GetLastWin32Error() + ")";
        else { CloseHandle(pi.hProcess); CloseHandle(pi.hThread); }

        if (env != IntPtr.Zero) DestroyEnvironmentBlock(env);
        return ok;
    }
}
