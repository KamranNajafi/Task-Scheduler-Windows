using System.Security.Cryptography;
using System.Text;

namespace TaskDelegator.Services;

/// <summary>
/// Stores the optional "run as account" password encrypted with DPAPI
/// (LocalMachine scope). The blob file is ACL'd to SYSTEM + Administrators only
/// (see DelegationManager.Harden), so standard users cannot read or decrypt it.
/// </summary>
public static class CredentialStore
{
    // App-specific entropy mixed into the DPAPI protection.
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("TaskDelegator/AppDelegation/v1");

    public static void Save(string file, string password)
    {
        var plain = Encoding.UTF8.GetBytes(password);
        var prot = ProtectedData.Protect(plain, Entropy, DataProtectionScope.LocalMachine);
        File.WriteAllBytes(file, prot);
        Array.Clear(plain, 0, plain.Length);
    }

    public static string Load(string file)
    {
        var prot = File.ReadAllBytes(file);
        var plain = ProtectedData.Unprotect(prot, Entropy, DataProtectionScope.LocalMachine);
        try { return Encoding.UTF8.GetString(plain); }
        finally { Array.Clear(plain, 0, plain.Length); }
    }
}
