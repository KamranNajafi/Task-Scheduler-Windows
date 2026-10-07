namespace TaskDelegator.Models;

/// <summary>Run-as identity for the delegated application.</summary>
public enum RunAsMode
{
    /// <summary>Launch as SYSTEM (elevated) into the user's session. No password needed.</summary>
    System = 0,
    /// <summary>Launch as a specific administrator account (credentials stored via DPAPI).</summary>
    Account = 1
}

/// <summary>An installed application discovered from the uninstall registry.</summary>
public sealed class InstalledApp
{
    public string DisplayName { get; set; } = "";
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public string? InstallLocation { get; set; }
    public string? Executable { get; set; }

    public override string ToString() => DisplayName;
}

/// <summary>A local Windows user account.</summary>
public sealed class LocalUserInfo
{
    public string Name { get; set; } = "";
    public string? FullName { get; set; }
    public bool Enabled { get; set; }
    public string Sid { get; set; } = "";
    public bool IsAdmin { get; set; }

    public string Role => IsAdmin ? "Admin" : "Standard";
    public override string ToString() => Name;
}

/// <summary>
/// Persisted descriptor for one delegation. Written by the GUI; read by the
/// SYSTEM launcher (--launch) so it knows which app to start and how.
/// </summary>
public sealed class DelegationConfig
{
    public string TaskName { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public string AppPath { get; set; } = "";
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public RunAsMode Mode { get; set; } = RunAsMode.System;

    /// <summary>For Account mode: "DOMAIN\\user" the app runs as.</summary>
    public string? AccountUser { get; set; }
    /// <summary>For Account mode: path to the DPAPI-protected password blob.</summary>
    public string? CredentialFile { get; set; }

    public string TargetUser { get; set; } = "";
    public string TargetUserSid { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string CreatedAtUtc { get; set; } = "";
}

/// <summary>A row shown in the "Existing delegations" list.</summary>
public sealed class DelegationRow
{
    public string TaskName { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public string App { get; set; } = "";
    public string RunAs { get; set; } = "";
    public string TargetUser { get; set; } = "";
    public string LastRun { get; set; } = "";
}
