@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Windows.ps1" -Action install -Channel public %*
set "S1DS_SETUP_EXIT=%ERRORLEVEL%"
pause
exit /b %S1DS_SETUP_EXIT%
