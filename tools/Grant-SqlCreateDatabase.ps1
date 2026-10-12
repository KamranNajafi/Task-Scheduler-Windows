<#
.SYNOPSIS
  Lets a delegated SQL Server tool (e.g. SQL Server Management Studio) create
  databases, by granting its login the needed server role on every LOCAL SQL
  Server instance. Fixes "CREATE DATABASE permission denied in database 'master'"
  (SQL Server Error 262).

.DESCRIPTION
  Task Delegator runs a delegated app in SYSTEM mode as NT AUTHORITY\SYSTEM.
  Since SQL Server 2012 that login is NOT a sysadmin by default, so it can connect
  but cannot CREATE DATABASE (Error 262). Run this script ONCE to grant the login
  the "dbcreator" role (create databases only, the default) or "sysadmin" (full
  admin) on each local instance.

  Requirements:
    * Run it in an ELEVATED PowerShell (Run as administrator).
    * The Windows account you run it as must already be a SQL Server sysadmin on
      the instance — normally the account that installed SQL Server.
    * sqlcmd must be available (it ships with SQL Server / the SQL command-line
      tools / SSMS). If it is missing, run the T-SQL shown at the end by hand in
      SSMS instead.

  Security note: granting "sysadmin" to NT AUTHORITY\SYSTEM means ANY process
  running as SYSTEM on this machine gets full SQL control. In a teaching lab that
  is usually acceptable; "dbcreator" is the narrower, safer choice.

.PARAMETER Login
  The Windows login to grant. Default "NT AUTHORITY\SYSTEM" (what SYSTEM-mode
  delegations run as). Use "DOMAIN\user" (or "PCNAME\user") for an Admin-account
  delegation instead.

.PARAMETER Role
  "dbcreator" (default — create databases only) or "sysadmin" (full admin rights).

.PARAMETER Instance
  Optional: target only this instance (e.g. ".\SQLEXPRESS" or "localhost").
  If omitted, every local instance found in the registry is updated.

.EXAMPLE
  .\Grant-SqlCreateDatabase.ps1
  Grants NT AUTHORITY\SYSTEM the dbcreator role on all local instances.

.EXAMPLE
  .\Grant-SqlCreateDatabase.ps1 -Role sysadmin

.EXAMPLE
  .\Grant-SqlCreateDatabase.ps1 -Login "LAB-PC\Teacher" -Role sysadmin -Instance ".\SQLEXPRESS"
#>
[CmdletBinding()]
param(
    [string]$Login = 'NT AUTHORITY\SYSTEM',
    [ValidateSet('dbcreator', 'sysadmin')][string]$Role = 'dbcreator',
    [string]$Instance
)

$ErrorActionPreference = 'Stop'

function Get-LocalSqlInstances {
    $key = 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL'
    if (-not (Test-Path $key)) { return @() }
    $props = Get-ItemProperty -Path $key
    $skip = 'PSPath', 'PSParentPath', 'PSChildName', 'PSDrive', 'PSProvider'
    $names = @()
    foreach ($p in $props.PSObject.Properties) {
        if ($skip -contains $p.Name) { continue }
        if ($p.Name -eq 'MSSQLSERVER') { $names += '.' }        # default instance
        else { $names += ".\$($p.Name)" }                       # named instance
    }
    return $names
}

# Reject obviously unsafe identifiers (Windows account names never contain these).
if ($Login -match "[\]';]") {
    Write-Error "The -Login value contains characters that are not allowed: $Login"
    exit 1
}

$tsql = @"
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$Login')
    CREATE LOGIN [$Login] FROM WINDOWS;
ALTER SERVER ROLE [$Role] ADD MEMBER [$Login];
"@

Write-Host "Task Delegator — grant '$Login' the '$Role' role so it can create databases." -ForegroundColor White
Write-Host ""

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    Write-Warning "sqlcmd was not found on this PC."
    Write-Host "Open SSMS as a SQL administrator and run this once instead:`n" -ForegroundColor Yellow
    Write-Host $tsql
    exit 1
}

$instances = if ($Instance) { @($Instance) } else { Get-LocalSqlInstances }
if (-not $instances -or $instances.Count -eq 0) {
    Write-Warning "No local SQL Server instance was found in the registry."
    Write-Host "If SQL Server is on another machine, run this T-SQL there (as a sysadmin):`n" -ForegroundColor Yellow
    Write-Host $tsql
    exit 1
}

$anyFail = $false
foreach ($inst in $instances) {
    Write-Host ("Granting on {0} ..." -f $inst) -ForegroundColor Cyan
    & $sqlcmd.Path -S $inst -E -b -Q $tsql
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  OK" -ForegroundColor Green
    }
    else {
        $anyFail = $true
        Write-Warning "  Failed on $inst (sqlcmd exit $LASTEXITCODE). Are you a SQL sysadmin on this instance, and is it running?"
    }
}

Write-Host ""
if ($anyFail) {
    Write-Warning "Finished with errors. For any instance above, you can run the T-SQL by hand in SSMS:"
    Write-Host $tsql
}
else {
    Write-Host "Done. In SQL Server Management Studio, DISCONNECT and reconnect (or restart it)" -ForegroundColor Green
    Write-Host "so the new role takes effect, then try creating the database again." -ForegroundColor Green
}
