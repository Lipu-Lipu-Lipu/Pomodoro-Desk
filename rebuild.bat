@echo off
rem ============================================================
rem  Rebuild the Pomodoro Timer desktop app and run its selftest.
rem
rem  The built-in selftest (--selftest) needs no GUI. It prints
rem  24 PASS lines plus "ALL PASS", and is the fastest way to tell
rem  whether the CODE is healthy.
rem
rem  This does a Release publish into publish\ (same as release.bat),
rem  because publish\ is what the desktop shortcut and end users run.
rem ============================================================

setlocal
cd /d "%~dp0"

echo === 1/2 Building (Release -> publish\) ===
dotnet publish -c Release -o publish --nologo
if errorlevel 1 (
    echo.
    echo [FAILED] Build did not pass.
    pause
    exit /b 1
)

echo.
echo === 2/2 Selftest ===
pushd "%~dp0publish"
PomodoroTimer.exe --selftest
set RC=%errorlevel%
popd

echo.
if "%RC%"=="0" (
    echo [OK] All selftest checks passed.
) else (
    echo [FAILED] Selftest exit code = %RC%
)
pause
endlocal
