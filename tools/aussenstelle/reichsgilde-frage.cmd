@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0frage.ps1" %*
exit /b %ERRORLEVEL%
