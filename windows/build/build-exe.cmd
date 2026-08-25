@echo off
REM Double-click this file to build KeyboardLanguageFix.exe.
REM
REM It just runs build-exe.ps1 with PowerShell, bypassing the execution policy
REM for this one process so a downloaded copy of the repository works without
REM the user having to change any Windows settings.

setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-exe.ps1" %*
set "EXITCODE=%ERRORLEVEL%"

echo.
pause
exit /b %EXITCODE%
