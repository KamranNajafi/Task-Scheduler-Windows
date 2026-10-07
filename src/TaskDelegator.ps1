<#
.SYNOPSIS
    Task Scheduler - Privileged Application Delegation (Windows)

.DESCRIPTION
    An IT-administration tool that lets an administrator delegate the right to run
    ONE specific, approved application with elevated (administrator) privileges to a
    specific standard user -- WITHOUT giving that user the administrator password.

    This is the Windows equivalent of a Linux `sudo` NOPASSWD rule. It is built on the
    sanctioned Windows Task Scheduler mechanism, not on any exploit or UAC bypass:

      1. This tool must be run elevated (it self-elevates via UAC). Only an admin who
         already knows the password can configure a delegation. That UAC prompt IS the
         "admin login".
      2. For an approved app it registers an on-demand Scheduled Task under the folder
         \AppDelegation\, set to "Run with highest privileges", running as a stored
         administrator identity.
      3. It grants the chosen standard user permission to TRIGGER (run) that one task,
         via the task's security descriptor (SDDL). The user can run the task; they
         cannot read or change the stored credentials, and they cannot run anything
         else elevated.
      4. It drops a desktop shortcut on the target user's desktop that calls
         `schtasks /run` for that task.

    When the standard user double-clicks the shortcut, Task Scheduler launches the
    approved app elevated. No password is shown or needed by the user.

    SECURITY NOTE: delegating an application is equivalent to granting `sudo` on it.
    Only delegate applications you trust. An app that can open a shell, run arbitrary
    files, or load arbitrary code (browsers, Office, cmd, PowerShell, file managers,
    "Run..." dialogs, etc.) effectively grants broader elevation. Choose narrowly.

.NOTES
    Tested target: Windows 10 / Windows 11, Windows PowerShell 5.1 (built in) or
    PowerShell 7+. Requires administrator rights to configure (self-elevates).
    All actions are written to C:\ProgramData\AppDelegation\audit.log.
#>

#Requires -Version 5.1

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# region Constants
# ---------------------------------------------------------------------------
$Script:AppTitle   = 'Task Scheduler - Privileged App Delegation'
$Script:TaskFolder = 'AppDelegation'                         # Task Scheduler subfolder
$Script:TaskPrefix = 'Delegated'                             # task name prefix
$Script:DataDir    = Join-Path $env:ProgramData 'AppDelegation'
$Script:AuditLog   = Join-Path $Script:DataDir 'audit.log'

# Task Scheduler COM enum values
$Script:TASK_LOGON_PASSWORD      = 1
$Script:TASK_LOGON_INTERACTIVE   = 3
$Script:TASK_RUNLEVEL_HIGHEST    = 1
$Script:TASK_ACTION_EXEC         = 0
$Script:TASK_CREATE_OR_UPDATE    = 6
$Script:TASK_INSTANCES_PARALLEL  = 0

# ---------------------------------------------------------------------------
# region Self-elevation (the "admin login")
# ---------------------------------------------------------------------------
function Test-IsAdmin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($id)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Invoke-SelfElevate {
    # Relaunch this exact script elevated. The UAC prompt is where the administrator
    # authenticates -- a standard user cannot get past it, so only admins can configure.
    $hostExe = (Get-Process -Id $PID).Path
    if (-not $hostExe) { $hostExe = 'powershell.exe' }

    $scriptPath = $PSCommandPath
    if (-not $scriptPath) { $scriptPath = $MyInvocation.MyCommand.Definition }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName  = $hostExe
    $psi.Arguments = '-NoProfile -ExecutionPolicy Bypass -STA -File "{0}"' -f $scriptPath
    $psi.Verb      = 'runas'
    $psi.UseShellExecute = $true
    try {
        [System.Diagnostics.Process]::Start($psi) | Out-Null
    } catch {
        # User cancelled the UAC prompt (or no admin available).
        [System.Windows.Forms.MessageBox]::Show(
            'Administrator rights are required to configure delegations.' + [Environment]::NewLine +
            'The elevation prompt was cancelled or denied.',
            $Script:AppTitle, 'OK', 'Warning') | Out-Null
    }
}

# ---------------------------------------------------------------------------
# region Audit logging
# ---------------------------------------------------------------------------
function Initialize-DataDir {
    if (-not (Test-Path $Script:DataDir)) {
        New-Item -ItemType Directory -Path $Script:DataDir -Force | Out-Null
    }
}

function Write-Audit {
    param([string]$Message)
    try {
        Initialize-DataDir
        $who = '{0}\{1}' -f $env:USERDOMAIN, $env:USERNAME
        $line = '{0}  {1}  {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $who, $Message
        Add-Content -Path $Script:AuditLog -Value $line -Encoding UTF8
    } catch { }
}

# ---------------------------------------------------------------------------
# region Enumeration: installed software
# ---------------------------------------------------------------------------
function Get-InstalledSoftware {
    $roots = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )
    $items = foreach ($root in $roots) {
        Get-ItemProperty -Path $root -ErrorAction SilentlyContinue
    }

    $result = foreach ($i in $items) {
        if ([string]::IsNullOrWhiteSpace($i.DisplayName)) { continue }
        if ($i.SystemComponent -eq 1) { continue }
        if ($i.DisplayName -match '^(KB\d+|Update for|Security Update|Hotfix)') { continue }

        $exe = Resolve-AppExecutable -DisplayIcon $i.DisplayIcon -InstallLocation $i.InstallLocation

        [pscustomobject]@{
            DisplayName     = $i.DisplayName.Trim()
            Version         = $i.DisplayVersion
            Publisher       = $i.Publisher
            InstallLocation = $i.InstallLocation
            Executable      = $exe
        }
    }

    $result |
        Sort-Object DisplayName -Unique |
        Sort-Object DisplayName
}

function Resolve-AppExecutable {
    param([string]$DisplayIcon, [string]$InstallLocation)

    # 1) DisplayIcon usually points to the main executable ("C:\path\app.exe,0").
    if ($DisplayIcon) {
        $candidate = ($DisplayIcon -split ',')[0].Trim('"', ' ')
        if ($candidate -match '\.exe$' -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }

    # 2) Otherwise, look for a plausible main .exe inside the install location.
    if ($InstallLocation -and (Test-Path -LiteralPath $InstallLocation)) {
        $exes = Get-ChildItem -LiteralPath $InstallLocation -Filter *.exe -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -notmatch '(?i)(unins|setup|install|update|crash|report|helper|service)' }
        if ($exes) {
            # Prefer an exe whose name resembles the folder / largest file.
            $best = $exes | Sort-Object Length -Descending | Select-Object -First 1
            return $best.FullName
        }
    }

    return $null
}

# ---------------------------------------------------------------------------
# region Enumeration: local users
# ---------------------------------------------------------------------------
function Get-LocalUsersInfo {
    $adminMembers = @()
    try {
        $adminMembers = (Get-LocalGroupMember -Group 'Administrators' -ErrorAction Stop |
            ForEach-Object { ($_.Name -split '\\')[-1] })
    } catch {
        try {
            $grp = [ADSI]"WinNT://./Administrators,group"
            $adminMembers = @($grp.psbase.Invoke('Members') | ForEach-Object {
                $_.GetType().InvokeMember('Name', 'GetProperty', $null, $_, $null)
            })
        } catch { }
    }

    $users = $null
    try {
        $users = Get-LocalUser -ErrorAction Stop | ForEach-Object {
            [pscustomobject]@{
                Name    = $_.Name
                Full    = $_.FullName
                Enabled = $_.Enabled
                Sid     = $_.SID.Value
                IsAdmin = ($adminMembers -contains $_.Name)
            }
        }
    } catch {
        $users = Get-CimInstance Win32_UserAccount -Filter "LocalAccount=True" -ErrorAction SilentlyContinue |
            ForEach-Object {
                [pscustomobject]@{
                    Name    = $_.Name
                    Full    = $_.FullName
                    Enabled = -not $_.Disabled
                    Sid     = $_.SID
                    IsAdmin = ($adminMembers -contains $_.Name)
                }
            }
    }

    $users |
        Where-Object { $_.Name -notin @('WDAGUtilityAccount','DefaultAccount') } |
        Sort-Object Name
}

function Get-UserSid {
    param([string]$UserName)
    try {
        return (Get-LocalUser -Name $UserName -ErrorAction Stop).SID.Value
    } catch {
        $nt = New-Object System.Security.Principal.NTAccount($env:COMPUTERNAME, $UserName)
        return $nt.Translate([System.Security.Principal.SecurityIdentifier]).Value
    }
}

function Get-UserDesktopPath {
    param([string]$UserName)

    # Map the user's SID to their real profile path (handles relocated/renamed profiles).
    $sid = $null
    try { $sid = Get-UserSid -UserName $UserName } catch { }

    $profilePath = $null
    if ($sid) {
        $prof = Get-CimInstance Win32_UserProfile -Filter "SID='$sid'" -ErrorAction SilentlyContinue
        if ($prof) { $profilePath = $prof.LocalPath }
    }
    if (-not $profilePath) {
        $profilePath = Join-Path (Split-Path $env:PUBLIC -Parent) $UserName  # C:\Users\<user>
    }

    if (-not $profilePath -or -not (Test-Path -LiteralPath $profilePath)) {
        return $null   # profile does not exist (user never logged on) -> caller falls back
    }

    $oneDrive = Join-Path $profilePath 'OneDrive\Desktop'
    if (Test-Path -LiteralPath $oneDrive) { return $oneDrive }

    return (Join-Path $profilePath 'Desktop')   # may not exist yet; shortcut fn will create it
}

# ---------------------------------------------------------------------------
# region Core: create / remove delegation
# ---------------------------------------------------------------------------
function ConvertTo-SafeTaskName {
    param([string]$Text)
    $clean = ($Text -replace '[^\w\-]', '_')
    if ([string]::IsNullOrWhiteSpace($clean)) { $clean = 'App' }
    return ('{0}_{1}' -f $Script:TaskPrefix, $clean)
}

function New-DelegatedTask {
    <#
        Registers an on-demand, highest-privileges task that runs $Exe as $RunAsUser
        (stored credentials), and grants $GrantUserSid the right to run it.
    #>
    param(
        [Parameter(Mandatory)][string]$TaskName,
        [Parameter(Mandatory)][string]$Exe,
        [string]$Arguments,
        [string]$WorkDir,
        [Parameter(Mandatory)][string]$RunAsUser,
        [Parameter(Mandatory)][string]$RunAsPassword,
        [Parameter(Mandatory)][string]$GrantUserSid,
        [string]$Description = ''
    )

    $svc = New-Object -ComObject Schedule.Service
    $svc.Connect()

    $root = $svc.GetFolder('\')
    try { $folder = $root.GetFolder($Script:TaskFolder) }
    catch { $folder = $root.CreateFolder($Script:TaskFolder) }

    $td = $svc.NewTask(0)
    $td.RegistrationInfo.Description = $Description
    $td.RegistrationInfo.Author      = 'TaskDelegator'

    $td.Principal.UserId    = $RunAsUser
    $td.Principal.RunLevel  = $Script:TASK_RUNLEVEL_HIGHEST
    $td.Principal.LogonType = $Script:TASK_LOGON_PASSWORD

    $s = $td.Settings
    $s.Enabled                   = $true
    $s.AllowDemandStart          = $true
    $s.Hidden                    = $false
    $s.DisallowStartIfOnBatteries= $false
    $s.StopIfGoingOnBatteries    = $false
    $s.ExecutionTimeLimit        = 'PT0S'      # no time limit
    $s.MultipleInstances         = $Script:TASK_INSTANCES_PARALLEL
    $s.StartWhenAvailable        = $true

    $act = $td.Actions.Create($Script:TASK_ACTION_EXEC)
    $act.Path = $Exe
    if ($Arguments) { $act.Arguments = $Arguments }
    if ($WorkDir)   { $act.WorkingDirectory = $WorkDir }
    elseif (Test-Path -LiteralPath $Exe) { $act.WorkingDirectory = (Split-Path -LiteralPath $Exe -Parent) }

    # No triggers => on-demand only (runs only when explicitly launched).

    # Security descriptor: Administrators + SYSTEM get full control; the delegated user
    # gets Generic Read + Generic Execute (enough to RUN the task, nothing more).
    $sddl = 'O:BAG:BAD:(A;;GA;;;BA)(A;;GA;;;SY)(A;;GRGX;;;{0})' -f $GrantUserSid

    [void]$folder.RegisterTaskDefinition(
        $TaskName,
        $td,
        $Script:TASK_CREATE_OR_UPDATE,
        $RunAsUser,
        $RunAsPassword,
        $Script:TASK_LOGON_PASSWORD,
        $sddl
    )

    return ('\{0}\{1}' -f $Script:TaskFolder, $TaskName)
}

function New-DelegatedShortcut {
    <#
        Creates a hidden launcher (.vbs) + a desktop shortcut (.lnk) on the target
        user's desktop. The shortcut invokes `schtasks /run` for the delegated task,
        with no visible console window.
    #>
    param(
        [Parameter(Mandatory)][string]$TaskName,
        [Parameter(Mandatory)][string]$FriendlyName,
        [Parameter(Mandatory)][string]$AppExe,
        [Parameter(Mandatory)][string]$DesktopPath
    )

    Initialize-DataDir
    if (-not (Test-Path -LiteralPath $DesktopPath)) {
        New-Item -ItemType Directory -Path $DesktopPath -Force | Out-Null
    }

    # Hidden launcher: run schtasks with a hidden window (0) so no console flashes.
    $vbsPath = Join-Path $Script:DataDir ("run_{0}.vbs" -f $TaskName)
    $taskFull = '\{0}\{1}' -f $Script:TaskFolder, $TaskName
    $vbsLine = 'CreateObject("WScript.Shell").Run "schtasks /run /tn ""' + $taskFull + '""", 0, False'
    Set-Content -Path $vbsPath -Value $vbsLine -Encoding ASCII -Force

    # Make sure standard users can read/execute the launcher.
    try {
        $acl = Get-Acl -LiteralPath $vbsPath
        $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
            'BUILTIN\Users', 'ReadAndExecute', 'Allow')
        $acl.AddAccessRule($rule)
        Set-Acl -LiteralPath $vbsPath -AclObject $acl
    } catch { }

    $safeName = ($FriendlyName -replace '[\\/:*?"<>|]', '_')
    $lnkPath = Join-Path $DesktopPath ("{0}.lnk" -f $safeName)

    $wsh = New-Object -ComObject WScript.Shell
    $sc = $wsh.CreateShortcut($lnkPath)
    $sc.TargetPath  = Join-Path $env:SystemRoot 'System32\wscript.exe'
    $sc.Arguments   = '//B //Nologo "{0}"' -f $vbsPath
    if ($AppExe -and (Test-Path -LiteralPath $AppExe)) { $sc.IconLocation = "$AppExe,0" }
    $sc.Description  = "Launch $FriendlyName (elevated, delegated by administrator)"
    $sc.WindowStyle  = 7
    $sc.Save()

    return $lnkPath
}

function Get-Delegations {
    # Enumerate tasks under the AppDelegation folder.
    $svc = New-Object -ComObject Schedule.Service
    $svc.Connect()
    try { $folder = $svc.GetFolder('\' + $Script:TaskFolder) }
    catch { return @() }

    $tasks = $folder.GetTasks(1)   # 1 = include hidden
    $out = foreach ($t in $tasks) {
        $xml = [xml]$t.Xml
        $exec = $xml.Task.Actions.Exec
        [pscustomobject]@{
            TaskName   = $t.Name
            RunAs      = $xml.Task.Principals.Principal.UserId
            App        = $exec.Command
            Arguments  = $exec.Arguments
            Enabled    = $t.Enabled
            LastRun    = $t.LastRunTime
        }
    }
    return @($out)
}

function Remove-Delegation {
    param([Parameter(Mandatory)][string]$TaskName)

    $svc = New-Object -ComObject Schedule.Service
    $svc.Connect()
    $folder = $svc.GetFolder('\' + $Script:TaskFolder)
    try { [void]$folder.DeleteTask($TaskName, 0) } catch { }

    # Remove the hidden launcher.
    $vbsPath = Join-Path $Script:DataDir ("run_{0}.vbs" -f $TaskName)
    if (Test-Path -LiteralPath $vbsPath) { Remove-Item -LiteralPath $vbsPath -Force -ErrorAction SilentlyContinue }

    # Remove any desktop shortcuts that point at this task (best effort, all users).
    $usersRoot = Split-Path $env:PUBLIC -Parent
    $desktops = Get-ChildItem -LiteralPath $usersRoot -Directory -ErrorAction SilentlyContinue |
        ForEach-Object {
            @((Join-Path $_.FullName 'Desktop'), (Join-Path $_.FullName 'OneDrive\Desktop'))
        }
    $desktops += (Join-Path $env:PUBLIC 'Desktop')
    foreach ($d in $desktops) {
        if (-not (Test-Path -LiteralPath $d)) { continue }
        Get-ChildItem -LiteralPath $d -Filter *.lnk -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                $wsh = New-Object -ComObject WScript.Shell
                $sc = $wsh.CreateShortcut($_.FullName)
                if ($sc.Arguments -match [regex]::Escape($TaskName)) {
                    Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
                }
            } catch { }
        }
    }
}

# ---------------------------------------------------------------------------
# region Entry point + GUI
# ---------------------------------------------------------------------------
# The GUI runs at *script scope* (not inside a function) so that WPF event
# handlers can reliably reach the shared variables ($ctl, $allSoftware, etc.).

Add-Type -AssemblyName System.Windows.Forms

# WPF requires a single-threaded apartment. Windows PowerShell 5.1 is STA by
# default; PowerShell 7+ (pwsh) is MTA, so relaunch ourselves with -STA. A plain
# relaunch (no 'runas') preserves the current elevation level.
if ([System.Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    $hostExe = (Get-Process -Id $PID).Path
    if (-not $hostExe) { $hostExe = 'powershell.exe' }
    $scriptPath = $PSCommandPath
    if (-not $scriptPath) { $scriptPath = $MyInvocation.MyCommand.Definition }
    Start-Process -FilePath $hostExe -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-STA', '-File', ('"{0}"' -f $scriptPath)
    ) | Out-Null
    return
}

# Admin gate: self-elevate (UAC) if not already elevated. This IS the "admin login".
if (-not (Test-IsAdmin)) {
    Invoke-SelfElevate
    return
}

Initialize-DataDir
Write-Audit "LAUNCHED (elevated) TaskDelegator GUI"

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

    [xml]$xaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Task Scheduler - Privileged App Delegation"
        Height="680" Width="900" WindowStartupLocation="CenterScreen"
        FontFamily="Segoe UI" FontSize="13" Background="#F3F3F3">
  <Grid Margin="12">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <Border Grid.Row="0" Background="#0F6CBD" CornerRadius="6" Padding="14" Margin="0,0,0,10">
      <StackPanel>
        <TextBlock Text="Privileged Application Delegation"
                   Foreground="White" FontSize="18" FontWeight="SemiBold"/>
        <TextBlock x:Name="AdminLine" Foreground="#DCECFF" Margin="0,4,0,0"
                   Text="Signed in as administrator"/>
      </StackPanel>
    </Border>

    <TabControl Grid.Row="1">
      <!-- TAB 1: create -->
      <TabItem Header="  Create delegation  ">
        <Grid Margin="10">
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="12"/>
            <ColumnDefinition Width="*"/>
          </Grid.ColumnDefinitions>
          <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
          </Grid.RowDefinitions>

          <!-- software -->
          <TextBlock Grid.Row="0" Grid.Column="0" Text="1. Installed application" FontWeight="SemiBold" Margin="0,0,0,4"/>
          <DockPanel Grid.Row="1" Grid.Column="0">
            <TextBox x:Name="SoftwareFilter" DockPanel.Dock="Top" Margin="0,0,0,4"
                     ToolTip="Type to filter"/>
            <ListView x:Name="SoftwareList">
              <ListView.View>
                <GridView>
                  <GridViewColumn Header="Application" Width="230" DisplayMemberBinding="{Binding DisplayName}"/>
                  <GridViewColumn Header="Version" Width="90" DisplayMemberBinding="{Binding Version}"/>
                </GridView>
              </ListView.View>
            </ListView>
          </DockPanel>

          <!-- users -->
          <TextBlock Grid.Row="0" Grid.Column="2" Text="2. Target Windows user" FontWeight="SemiBold" Margin="0,0,0,4"/>
          <ListView x:Name="UserList" Grid.Row="1" Grid.Column="2">
            <ListView.View>
              <GridView>
                <GridViewColumn Header="User" Width="150" DisplayMemberBinding="{Binding Name}"/>
                <GridViewColumn Header="Role" Width="90" DisplayMemberBinding="{Binding Role}"/>
                <GridViewColumn Header="Enabled" Width="70" DisplayMemberBinding="{Binding Enabled}"/>
              </GridView>
            </ListView.View>
          </ListView>

          <!-- settings -->
          <Border Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="3" Background="White"
                  BorderBrush="#DDD" BorderThickness="1" CornerRadius="6" Padding="12" Margin="0,12,0,0">
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
              </Grid.ColumnDefinitions>
              <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
              </Grid.RowDefinitions>

              <TextBlock Grid.Row="0" Grid.Column="0" Text="Executable:" VerticalAlignment="Center" Margin="0,0,8,6"/>
              <TextBox   Grid.Row="0" Grid.Column="1" x:Name="ExeBox" Margin="0,0,8,6"/>
              <Button    Grid.Row="0" Grid.Column="2" x:Name="BrowseBtn" Content="Browse..." Width="90" Margin="0,0,8,6"/>

              <TextBlock Grid.Row="1" Grid.Column="0" Text="Arguments:" VerticalAlignment="Center" Margin="0,0,8,6"/>
              <TextBox   Grid.Row="1" Grid.Column="1" x:Name="ArgsBox" Margin="0,0,8,6"/>
              <TextBlock Grid.Row="1" Grid.Column="2" Text="Shortcut name:" VerticalAlignment="Center" Margin="0,0,8,6"/>
              <TextBox   Grid.Row="1" Grid.Column="3" x:Name="ShortcutNameBox" Margin="0,0,0,6"/>

              <TextBlock Grid.Row="2" Grid.Column="0" Text="Run as admin:" VerticalAlignment="Center" Margin="0,0,8,6"/>
              <TextBox   Grid.Row="2" Grid.Column="1" x:Name="RunAsBox" Margin="0,0,8,6"
                         ToolTip="Administrator account the app runs under (credentials stored securely by Windows)."/>
              <TextBlock Grid.Row="2" Grid.Column="2" Text="Admin password:" VerticalAlignment="Center" Margin="0,0,8,6"/>
              <PasswordBox Grid.Row="2" Grid.Column="3" x:Name="RunAsPwdBox" Margin="0,0,0,6"/>

              <CheckBox Grid.Row="3" Grid.Column="1" Grid.ColumnSpan="3" x:Name="MakeShortcutChk"
                        Content="Create a shortcut on the user's desktop" IsChecked="True" Margin="0,2,0,0"/>
            </Grid>
          </Border>
        </Grid>
      </TabItem>

      <!-- TAB 2: manage -->
      <TabItem Header="  Existing delegations  ">
        <DockPanel Margin="10">
          <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="0,8,0,0">
            <Button x:Name="RefreshBtn" Content="Refresh" Width="100" Margin="0,0,8,0"/>
            <Button x:Name="RemoveBtn" Content="Remove selected" Width="140"/>
          </StackPanel>
          <ListView x:Name="DelegationList">
            <ListView.View>
              <GridView>
                <GridViewColumn Header="Task" Width="190" DisplayMemberBinding="{Binding TaskName}"/>
                <GridViewColumn Header="Application" Width="300" DisplayMemberBinding="{Binding App}"/>
                <GridViewColumn Header="Runs as" Width="160" DisplayMemberBinding="{Binding RunAs}"/>
                <GridViewColumn Header="Last run" Width="150" DisplayMemberBinding="{Binding LastRun}"/>
              </GridView>
            </ListView.View>
          </ListView>
        </DockPanel>
      </TabItem>
    </TabControl>

    <!-- footer -->
    <Grid Grid.Row="2" Margin="0,10,0,0">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
      </Grid.ColumnDefinitions>
      <TextBlock x:Name="StatusText" Grid.Column="0" VerticalAlignment="Center"
                 Foreground="#444" TextTrimming="CharacterEllipsis"/>
      <Button x:Name="CreateBtn" Grid.Column="1" Content="Create delegation"
              Width="180" Height="34" FontWeight="SemiBold"
              Background="#0F6CBD" Foreground="White" BorderThickness="0"/>
    </Grid>
  </Grid>
</Window>
'@

    $reader = New-Object System.Xml.XmlNodeReader $xaml
    $window = [Windows.Markup.XamlReader]::Load($reader)

    # --- find controls ---
    $ctl = @{}
    foreach ($n in 'AdminLine','SoftwareFilter','SoftwareList','UserList','ExeBox','ArgsBox',
                   'ShortcutNameBox','RunAsBox','RunAsPwdBox','MakeShortcutChk','CreateBtn',
                   'StatusText','BrowseBtn','DelegationList','RefreshBtn','RemoveBtn') {
        $ctl[$n] = $window.FindName($n)
    }

    # --- populate ---
    $ctl.AdminLine.Text = 'Signed in as administrator: {0}\{1}   (configuration requires elevation)' -f $env:USERDOMAIN, $env:USERNAME
    $ctl.RunAsBox.Text  = '{0}\{1}' -f $env:USERDOMAIN, $env:USERNAME

    $allSoftware = @(Get-InstalledSoftware)
    $ctl.SoftwareList.ItemsSource = $allSoftware

    $users = @(
        @(Get-LocalUsersInfo) | ForEach-Object {
            $_ | Add-Member -NotePropertyName Role -NotePropertyValue ($(if ($_.IsAdmin) {'Admin'} else {'Standard'})) -PassThru
        }
    )
    $ctl.UserList.ItemsSource = $users

    $setStatus = {
        param($msg, $isError = $false)
        $ctl.StatusText.Text = $msg
        if ($isError) { $ctl.StatusText.Foreground = [System.Windows.Media.Brushes]::DarkRed }
        else          { $ctl.StatusText.Foreground = [System.Windows.Media.Brushes]::DimGray }
    }

    # --- filter software ---
    $ctl.SoftwareFilter.Add_TextChanged({
        $q = $ctl.SoftwareFilter.Text
        if ([string]::IsNullOrWhiteSpace($q)) {
            $ctl.SoftwareList.ItemsSource = $allSoftware
        } else {
            $ctl.SoftwareList.ItemsSource = @($allSoftware | Where-Object { $_.DisplayName -like "*$q*" })
        }
    })

    # --- software selection -> fill exe + name ---
    $ctl.SoftwareList.Add_SelectionChanged({
        $sel = $ctl.SoftwareList.SelectedItem
        if ($sel) {
            if ($sel.Executable) { $ctl.ExeBox.Text = $sel.Executable }
            if ([string]::IsNullOrWhiteSpace($ctl.ShortcutNameBox.Text)) {
                $ctl.ShortcutNameBox.Text = $sel.DisplayName
            }
            if (-not $sel.Executable) {
                & $setStatus "No executable auto-detected for '$($sel.DisplayName)'. Use Browse... to pick the .exe." $true
            } else {
                & $setStatus ""
            }
        }
    })

    # --- browse ---
    $ctl.BrowseBtn.Add_Click({
        Add-Type -AssemblyName System.Windows.Forms
        $dlg = New-Object System.Windows.Forms.OpenFileDialog
        $dlg.Filter = 'Executables (*.exe)|*.exe|All files (*.*)|*.*'
        $dlg.InitialDirectory = ${env:ProgramFiles}
        if ($dlg.ShowDialog() -eq 'OK') {
            $ctl.ExeBox.Text = $dlg.FileName
            if ([string]::IsNullOrWhiteSpace($ctl.ShortcutNameBox.Text)) {
                $ctl.ShortcutNameBox.Text = [IO.Path]::GetFileNameWithoutExtension($dlg.FileName)
            }
        }
    })

    # --- manage tab ---
    $refreshDelegations = {
        $ctl.DelegationList.ItemsSource = @(Get-Delegations)
    }
    $ctl.RefreshBtn.Add_Click({ & $refreshDelegations })
    $ctl.RemoveBtn.Add_Click({
        $sel = $ctl.DelegationList.SelectedItem
        if (-not $sel) { & $setStatus "Select a delegation to remove." $true; return }
        $res = [System.Windows.MessageBox]::Show(
            "Remove delegation '$($sel.TaskName)' and its desktop shortcut(s)?",
            $Script:AppTitle, 'YesNo', 'Question')
        if ($res -eq 'Yes') {
            try {
                Remove-Delegation -TaskName $sel.TaskName
                Write-Audit ("REMOVED task={0}" -f $sel.TaskName)
                & $refreshDelegations
                & $setStatus "Removed '$($sel.TaskName)'."
            } catch {
                & $setStatus ("Remove failed: {0}" -f $_.Exception.Message) $true
            }
        }
    })

    # --- create ---
    $ctl.CreateBtn.Add_Click({
        try {
            $exe  = $ctl.ExeBox.Text.Trim()
            $user = $ctl.UserList.SelectedItem
            $runAs = $ctl.RunAsBox.Text.Trim()
            $pwd   = $ctl.RunAsPwdBox.Password
            $appArgs = $ctl.ArgsBox.Text.Trim()
            $name  = $ctl.ShortcutNameBox.Text.Trim()

            if (-not $exe -or -not (Test-Path -LiteralPath $exe)) { & $setStatus "Choose a valid executable (.exe)." $true; return }
            if (-not $user)  { & $setStatus "Select a target Windows user." $true; return }
            if (-not $runAs) { & $setStatus "Enter the administrator account to run the app as." $true; return }
            if (-not $pwd)   { & $setStatus "Enter the administrator password (stored securely by Windows)." $true; return }
            if (-not $name)  { $name = [IO.Path]::GetFileNameWithoutExtension($exe) }

            if ($user.IsAdmin) {
                $warn = [System.Windows.MessageBox]::Show(
                    "'$($user.Name)' is already an administrator and does not need delegation. Continue anyway?",
                    $Script:AppTitle, 'YesNo', 'Warning')
                if ($warn -ne 'Yes') { return }
            }

            & $setStatus "Creating delegation..."
            $taskName = ConvertTo-SafeTaskName -Text ("{0}_{1}" -f $name, $user.Name)
            $userSid  = Get-UserSid -UserName $user.Name

            $taskPath = New-DelegatedTask -TaskName $taskName -Exe $exe -Arguments $appArgs `
                -RunAsUser $runAs -RunAsPassword $pwd -GrantUserSid $userSid `
                -Description ("Delegated elevated launch of {0} for {1}" -f $name, $user.Name)

            Write-Audit ("CREATED task={0} app='{1}' args='{2}' runas={3} foruser={4}" -f $taskName, $exe, $appArgs, $runAs, $user.Name)

            $lnkInfo = ""
            if ($ctl.MakeShortcutChk.IsChecked) {
                $desktop = Get-UserDesktopPath -UserName $user.Name
                if ([string]::IsNullOrEmpty($desktop)) {
                    $desktop = Join-Path $env:PUBLIC 'Desktop'
                    $lnkInfo = " (user profile not found; shortcut placed on the Public desktop)"
                }
                $lnk = New-DelegatedShortcut -TaskName $taskName -FriendlyName $name -AppExe $exe -DesktopPath $desktop
                Write-Audit ("SHORTCUT {0}" -f $lnk)
            }

            $ctl.RunAsPwdBox.Password = ''
            & $setStatus ("Done. '{0}' can now run '{1}' elevated via the task {2}.{3}" -f $user.Name, $name, $taskPath, $lnkInfo)
            $doneMsg = "Delegation created." + [Environment]::NewLine + [Environment]::NewLine +
                       ("User '{0}' can now launch '{1}' with administrator rights." -f $user.Name, $name) +
                       [Environment]::NewLine + ("Task: {0}{1}" -f $taskPath, $lnkInfo)
            [System.Windows.MessageBox]::Show($doneMsg, $Script:AppTitle, 'OK', 'Information') | Out-Null
        }
        catch {
            & $setStatus ("Failed: {0}" -f $_.Exception.Message) $true
            [System.Windows.MessageBox]::Show($_.Exception.Message, $Script:AppTitle, 'OK', 'Error') | Out-Null
        }
    })

    & $refreshDelegations
    [void]$window.ShowDialog()
