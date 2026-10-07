@echo off
REM ===========================================================================
REM  Task Scheduler - Privileged App Delegation : launcher
REM ---------------------------------------------------------------------------
REM  Double-click this file. It starts the tool; the tool then requests
REM  administrator elevation (UAC). That UAC prompt is the "admin login" -- only
REM  an administrator can configure delegations.
REM ===========================================================================

setlocal
set "SCRIPT=%~dp0..\src\TaskDelegator.ps1"

REM Prefer PowerShell 7+ (pwsh) if present, else Windows PowerShell.
where pwsh >nul 2>&1
if %errorlevel%==0 (
    start "" pwsh -NoProfile -ExecutionPolicy Bypass -STA -File "%SCRIPT%"
) else (
    start "" powershell -NoProfile -ExecutionPolicy Bypass -STA -File "%SCRIPT%"
)

endlocal
