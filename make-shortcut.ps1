# Rebuild the desktop shortcut for the Pomodoro Timer.
#
# Run this from PowerShell. It recreates "Pomodoro Timer.lnk" on the Desktop.
#
# Target choice
# ------------
# The shortcut points DIRECTLY at the published exe in publish\.
#   * Not at a .bat launcher: a .bat is hosted by cmd.exe, which always gets a
#     console window -- the user sees it flash for a moment on every launch.
#   * Not at bin\Debug\: that is a debug build. This is the finished desktop app,
#     so the Release output (publish\) is the right thing to run.
#
# History note (2026-09-29)
# -------------------------
# Earlier the same day, publish\ and bin\Release\ were blocked by a Windows
# application control policy (WDAC / CodeIntegrity), failing with:
#      System.IO.FileLoadException:
#      "this file is blocked by application control policy" (0x800711C7)
# The same DLL bytes were blocked in one directory but allowed in another, so it
# was a path allowlist, not a content issue -- and bin\Debug\ was the only output
# that ran. That restriction has since lifted: all three outputs now run.
# If publish\ ever gets blocked again, fall back to bin\Debug\ (change $target).
#
# If the exe is missing, run rebuild.bat first.

$ErrorActionPreference = 'Stop'

$desk = [Environment]::GetFolderPath('Desktop')
# $PSScriptRoot is already the project directory ("Pomodoro Timer Desk").
$project = $PSScriptRoot

$target = Join-Path $project 'publish\PomodoroTimer.exe'

if (-not (Test-Path -LiteralPath $target)) {
    throw "Executable not found: $target`nRun rebuild.bat (or publish.bat) first to produce it."
}

$lnkPath = Join-Path $desk 'Pomodoro Timer.lnk'
$ws  = New-Object -ComObject WScript.Shell
$lnk = $ws.CreateShortcut($lnkPath)

$lnk.TargetPath       = $target
$lnk.WorkingDirectory = Split-Path -Parent $target
$lnk.Description      = 'Pomodoro Timer desktop app'
$lnk.IconLocation     = (Join-Path $project 'tomato.ico') + ',0'

$lnk.Save()

if (Test-Path -LiteralPath $lnkPath) {
    Write-Output ("OK   " + $lnkPath)
    Write-Output ("  -> " + $target)
} else {
    Write-Output ("FAIL " + $lnkPath)
    exit 1
}
