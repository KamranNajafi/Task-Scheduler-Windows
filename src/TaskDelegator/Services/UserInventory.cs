using System.Management;
using TaskDelegator.Models;
using TaskDelegator.Native;

namespace TaskDelegator.Services;

/// <summary>Enumerates local Windows user accounts and flags administrators.</summary>
public static class UserInventory
{
    public static List<LocalUserInfo> GetAll()
    {
        HashSet<string> adminSids;
        try { adminSids = NativeMethods.GetAdministratorMemberSids(); }
        catch { adminSids = new HashSet<string>(); }

        var list = new List<LocalUserInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2",
                "SELECT Name, FullName, Disabled, SID FROM Win32_UserAccount WHERE LocalAccount=TRUE");

            foreach (ManagementObject mo in searcher.Get())
            {
                var name = mo["Name"] as string ?? "";
                if (name is "WDAGUtilityAccount" or "DefaultAccount") continue;

                var sid = mo["SID"] as string ?? "";
                list.Add(new LocalUserInfo
                {
                    Name = name,
                    FullName = mo["FullName"] as string,
                    Enabled = !(mo["Disabled"] as bool? ?? false),
                    Sid = sid,
                    IsAdmin = sid.Length > 0 && adminSids.Contains(sid)
                });
            }
        }
        catch (Exception ex)
        {
            AuditLog.Write("UserInventory error: " + ex.Message);
        }

        return list.OrderBy(u => u.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
