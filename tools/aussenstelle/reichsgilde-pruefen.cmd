@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0pruefen.ps1" %*
exit /b %ERRORLEVEL%
