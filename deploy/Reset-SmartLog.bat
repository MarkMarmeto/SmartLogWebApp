@echo off
REM ============================================================
REM SmartLog Web App - Reset Installation Launcher
REM Removes service, env vars, firewall rules for a fresh setup.
REM Right-click this file and select "Run as administrator".
REM ============================================================

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: This script must be run as Administrator.
    echo Right-click and select "Run as administrator".
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Reset-SmartLog.ps1"
pause
