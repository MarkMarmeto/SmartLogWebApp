@echo off
REM ============================================================
REM SmartLog -- HTTPS Setup (Self-Signed Certificate)
REM Run this script as Administrator
REM
REM Usage:
REM   Setup-Https.bat                    (uses defaults: HTTPS=5051, HTTP=5050)
REM   Setup-Https.bat 5443 5080          (custom HTTPS port 5443, HTTP port 5080)
REM
REM Arguments:
REM   %1  HTTPS port  (default: 5051)
REM   %2  HTTP port   (default: 5050)
REM ============================================================

echo.
echo SmartLog HTTPS Setup
echo ============================================================
echo This will generate a self-signed SSL certificate and
echo configure SmartLog to serve HTTPS on the specified port.
echo.

SET HTTPS_PORT=5051
SET HTTP_PORT=5050

IF NOT "%~1"=="" SET HTTPS_PORT=%~1
IF NOT "%~2"=="" SET HTTP_PORT=%~2

echo HTTPS port : %HTTPS_PORT%
echo HTTP port  : %HTTP_PORT%
echo.
pause

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup-Https.ps1" -HttpsPort %HTTPS_PORT% -HttpPort %HTTP_PORT%

pause
