@echo off
REM ============================================================
REM SmartLog — HTTPS Setup (Self-Signed Certificate)
REM Run this script as Administrator
REM ============================================================
echo.
echo SmartLog HTTPS Setup
echo ============================================================
echo This will generate a self-signed SSL certificate and
echo configure SmartLog to serve HTTPS on port 5051.
echo.
pause

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup-Https.ps1"

pause
