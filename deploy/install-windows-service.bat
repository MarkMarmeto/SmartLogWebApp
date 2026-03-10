@echo off
REM ============================================================
REM SmartLog Web - Windows Service Installer
REM Run this script as Administrator
REM ============================================================

setlocal

set SERVICE_NAME=SmartLogWeb
set SERVICE_DISPLAY=SmartLog Web Application
set INSTALL_DIR=C:\SmartLog
set PROJECT_DIR=%~dp0..\src\SmartLog.Web

REM ============================================================
REM Check for admin privileges
REM ============================================================
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: This script must be run as Administrator.
    echo Right-click and select "Run as administrator".
    pause
    exit /b 1
)

REM ============================================================
REM Step 1: Publish the application
REM ============================================================
echo.
echo [1/4] Publishing SmartLog Web (win-x64, self-contained)...
dotnet publish "%PROJECT_DIR%" -p:PublishProfile=WinX64 -o "%INSTALL_DIR%"
if %errorlevel% neq 0 (
    echo ERROR: Publish failed.
    pause
    exit /b 1
)
echo       Published to %INSTALL_DIR%

REM ============================================================
REM Step 2: Stop existing service if running
REM ============================================================
echo.
echo [2/4] Checking for existing service...
sc query %SERVICE_NAME% >nul 2>&1
if %errorlevel% equ 0 (
    echo       Stopping existing service...
    sc stop %SERVICE_NAME% >nul 2>&1
    timeout /t 5 /nobreak >nul
    echo       Removing existing service...
    sc delete %SERVICE_NAME% >nul 2>&1
    timeout /t 2 /nobreak >nul
)

REM ============================================================
REM Step 3: Create the Windows Service
REM ============================================================
echo.
echo [3/4] Creating Windows Service...
sc create %SERVICE_NAME% ^
    binPath="%INSTALL_DIR%\SmartLog.Web.exe" ^
    DisplayName="%SERVICE_DISPLAY%" ^
    start=auto ^
    obj=LocalSystem
if %errorlevel% neq 0 (
    echo ERROR: Failed to create service.
    pause
    exit /b 1
)

REM Set service description
sc description %SERVICE_NAME% "SmartLog School Information Management System - Attendance tracking, SMS notifications, and administrative tools."

REM Configure recovery: restart on first, second, and subsequent failures
sc failure %SERVICE_NAME% reset=86400 actions=restart/5000/restart/10000/restart/30000

REM ============================================================
REM Step 4: Configure environment variables and start
REM ============================================================
echo.
echo [4/4] Starting service...

REM IMPORTANT: Set these environment variables before starting.
REM You can set them system-wide via:
REM   setx /M SMARTLOG_DB_CONNECTION "Server=.\SQLEXPRESS;Database=SmartLog;Trusted_Connection=true;TrustServerCertificate=true"
REM   setx /M SMARTLOG_HMAC_SECRET_KEY "your-secret-key-here"
REM   setx /M SMARTLOG_SEED_PASSWORD "YourInitialAdminPassword1!"
REM   setx /M ASPNETCORE_URLS "http://+:5050"

sc start %SERVICE_NAME%
if %errorlevel% neq 0 (
    echo WARNING: Service failed to start. Check environment variables are set.
    echo.
    echo Required environment variables (set via setx /M):
    echo   SMARTLOG_DB_CONNECTION    - SQL Server connection string
    echo   SMARTLOG_HMAC_SECRET_KEY  - QR code HMAC signing secret
    echo   SMARTLOG_SEED_PASSWORD    - Initial admin password
    echo   ASPNETCORE_URLS           - Listen URL (e.g., http://+:5050)
    pause
    exit /b 1
)

echo.
echo ============================================================
echo SUCCESS: %SERVICE_NAME% installed and started.
echo.
echo   Service Name:  %SERVICE_NAME%
echo   Install Path:  %INSTALL_DIR%
echo   Status:        sc query %SERVICE_NAME%
echo   Logs:          Event Viewer ^> Windows Logs ^> Application
echo.
echo USEFUL COMMANDS:
echo   Stop:          sc stop %SERVICE_NAME%
echo   Start:         sc start %SERVICE_NAME%
echo   Restart:       sc stop %SERVICE_NAME% ^& timeout /t 3 ^& sc start %SERVICE_NAME%
echo   Uninstall:     sc stop %SERVICE_NAME% ^& sc delete %SERVICE_NAME%
echo ============================================================
pause
