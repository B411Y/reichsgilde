@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0reichsgilde-stopp.ps1" %*
exit /b %ERRORLEVEL%
