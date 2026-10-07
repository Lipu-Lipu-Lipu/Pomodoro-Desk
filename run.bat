@echo off
rem ============================================================
rem  Launch the Pomodoro Timer desktop app (fallback launcher).
rem
rem  NOTE: the desktop shortcut does NOT use this script. It points straight at
rem  publish\PomodoroTimer.exe, because launching a .bat always opens a console
rem  window that visibly flashes on screen for a moment. This script is only a
rem  convenience for command-line use.
rem
rem  If publish\ is empty, this publishes it first. It deliberately does NOT call
rem  rebuild.bat, because that script ends with `pause` and would hang whenever
rem  this is invoked non-interactively.
rem ============================================================

setlocal
cd /d "%~dp0"

if not exist "publish\PomodoroTimer.exe" (
    echo [Pomodoro] publish\ not found, building it now...
    dotnet publish -c Release -o publish --nologo
    if not exist "publish\PomodoroTimer.exe" (
        echo [Pomodoro] Build failed. See the errors above.
        pause
        exit /b 1
    )
)

cd /d "%~dp0publish"
rem Launch detached so closing this console does not kill the app.
start "" "PomodoroTimer.exe"
endlocal
