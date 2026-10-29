@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0reichsgilde-frei.ps1" %*
exit /b %ERRORLEVEL%
