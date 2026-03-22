@echo off
REM SmartLog - HMAC Secret Key Generator
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0GenerateHMAC.ps1"
