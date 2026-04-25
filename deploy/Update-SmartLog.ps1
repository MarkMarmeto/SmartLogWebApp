#Requires -RunAsAdministrator
<#
.SYNOPSIS
    SmartLog Web App — Update Script for Windows

.DESCRIPTION
    Automates the update process for an existing SmartLog Web App installation:
    - Backs up current installation
    - Pulls latest code from GitHub
    - Rebuilds and publishes the application
    - Restarts the Windows Service

    Safe to run multiple times. Creates timestamped backups.

.NOTES
    Run this script as Administrator in PowerShell.
    Usage: .\Update-SmartLog.ps1

    Optional parameters:
      -SkipBackup    Skip creating a backup before updating
      -Branch        Git branch to pull from (default: main)
#>

[CmdletBinding()]
param(
    [switch]$SkipBackup,
    [string]$Branch = "main"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ============================================================
# Configuration
# ============================================================
$Script:ServiceName = "SmartLogWeb"
$Script:InstallDir  = "C:\SmartLog"
$Script:BackupDir   = "C:\SmartLogBackups"
$Script:LogDir      = "C:\SmartLog\logs"

# ============================================================
# Helper Functions
# ============================================================
function Write-StepHeader {
    param([int]$Step, [int]$Total, [string]$Title)
    Write-Host ""
    Write-Host "  [$Step/$Total] $Title" -ForegroundColor Cyan
    Write-Host "  $('=' * 50)" -ForegroundColor DarkGray
}

function Write-Success {
    param([string]$Message)
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "  [!!] $Message" -ForegroundColor Yellow
}

function Write-Fail {
    param([string]$Message)
    Write-Host "  [X]  $Message" -ForegroundColor Red
}

function Write-Detail {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor Gray
}

# ============================================================
# Banner
# ============================================================
Clear-Host
Write-Host ""
Write-Host "  ======================================================" -ForegroundColor Cyan
Write-Host "       SmartLog Web App -- Update Tool                   " -ForegroundColor Cyan
Write-Host "  ======================================================" -ForegroundColor Cyan
Write-Host ""

$totalSteps = 6

# ============================================================
# Step 1: Validate Existing Installation
# ============================================================
Write-StepHeader -Step 1 -Total $totalSteps -Title "Validating Installation"

# Check service exists
$service = Get-Service -Name $Script:ServiceName -ErrorAction SilentlyContinue
if (-not $service) {
    Write-Fail "Windows Service '$($Script:ServiceName)' not found."
    Write-Host "  Run Setup-SmartLog.ps1 for first-time installation." -ForegroundColor Yellow
    Read-Host "  Press Enter to exit"
    exit 1
}
Write-Success "Service '$($Script:ServiceName)' found (Status: $($service.Status))"

# Check install directory
if (-not (Test-Path $Script:InstallDir)) {
    Write-Fail "Install directory not found: $($Script:InstallDir)"
    Read-Host "  Press Enter to exit"
    exit 1
}
Write-Success "Install directory exists: $($Script:InstallDir)"

# Find repo root - check common locations
$repoRoot = $null
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$possibleRoot = Split-Path -Parent $scriptDir

if (Test-Path (Join-Path $possibleRoot "src\SmartLog.Web\SmartLog.Web.csproj")) {
    $repoRoot = $possibleRoot
}
elseif (Test-Path "C:\SmartLogWebApp\src\SmartLog.Web\SmartLog.Web.csproj") {
    $repoRoot = "C:\SmartLogWebApp"
}

if (-not $repoRoot) {
    Write-Warn "Could not auto-detect repository location."
    $repoRoot = Read-Host "  Enter the path to SmartLogWebApp repository"
    if (-not (Test-Path (Join-Path $repoRoot "src\SmartLog.Web\SmartLog.Web.csproj"))) {
        Write-Fail "SmartLog.Web.csproj not found at $repoRoot\src\SmartLog.Web\"
        Read-Host "  Press Enter to exit"
        exit 1
    }
}
Write-Success "Repository found: $repoRoot"

# ============================================================
# Step 2: Check for Updates
# ============================================================
Write-StepHeader -Step 2 -Total $totalSteps -Title "Checking for Updates"

Push-Location $repoRoot
try {
    # Git writes info to stderr which PowerShell treats as errors with $ErrorActionPreference = "Stop"
    $prevEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"

    # Fetch latest from remote
    Write-Detail "Fetching from remote..."
    git fetch origin $Branch 2>&1 | Out-Null

    # Check if there are updates
    $localHash = (git rev-parse HEAD 2>&1) | Select-Object -Last 1
    $remoteHash = (git rev-parse "origin/$Branch" 2>&1) | Select-Object -Last 1

    $ErrorActionPreference = $prevEAP

    if ($localHash -eq $remoteHash) {
        Write-Success "Already up to date (commit: $($localHash.Substring(0, 7)))"
        Write-Host ""
        $continue = Read-Host "  No new changes found. Rebuild anyway? (y/N)"
        if ($continue -notmatch '^[Yy]') {
            Write-Host ""
            Write-Host "  No update needed. Exiting." -ForegroundColor Gray
            Pop-Location
            Read-Host "  Press Enter to exit"
            exit 0
        }
    }
    else {
        # Show what's new
        $ErrorActionPreference = "Continue"
        $commitCount = (git rev-list --count "$localHash..$remoteHash" 2>&1) | Select-Object -Last 1
        Write-Success "$commitCount new commit(s) available"
        Write-Host ""
        Write-Host "  Recent changes:" -ForegroundColor Gray
        git log --oneline "$localHash..$remoteHash" 2>&1 | Select-Object -First 10 | ForEach-Object {
            Write-Host "    $_" -ForegroundColor White
        }
        $ErrorActionPreference = $prevEAP
        Write-Host ""
    }
}
catch {
    $ErrorActionPreference = $prevEAP
    Write-Warn "Could not check for updates: $_"
    Write-Detail "Continuing with rebuild..."
}

# ============================================================
# Step 3: Backup Application & Database
# ============================================================
Write-StepHeader -Step 3 -Total $totalSteps -Title "Backing Up Application & Database"

if ($SkipBackup) {
    Write-Detail "Backup skipped (--SkipBackup flag)"
}
else {
    if (-not (Test-Path $Script:BackupDir)) {
        New-Item -ItemType Directory -Path $Script:BackupDir -Force | Out-Null
    }

    $timestamp = Get-Date -Format "yyyy-MM-dd_HHmmss"
    $backupPath = Join-Path $Script:BackupDir "smartlog-backup-$timestamp"

    # -- Application Backup --
    Write-Detail "Backing up application files..."
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
    Get-ChildItem $Script:InstallDir -Exclude "logs" | Copy-Item -Destination $backupPath -Recurse -Force

    $backupSize = [math]::Round((Get-ChildItem $backupPath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
    Write-Success "Application backed up ($($backupSize) MB) → $backupPath"

    # Clean up old app backups (keep last 5)
    # @() forces an array so .Count is always available even when only one item is returned
    $oldBackups = @(Get-ChildItem $Script:BackupDir -Directory -Filter "smartlog-backup-*" | Sort-Object Name -Descending | Select-Object -Skip 5)
    if ($oldBackups.Count -gt 0) {
        $oldBackups | Remove-Item -Recurse -Force
        Write-Detail "Cleaned up $($oldBackups.Count) old app backup(s)"
    }

    # -- Database Backup --
    Write-Detail "Backing up SQL Server database..."

    # Resolve connection string: env var takes priority, then installed appsettings.json
    $connStr = [System.Environment]::GetEnvironmentVariable("SMARTLOG_DB_CONNECTION", "Machine")
    if (-not $connStr) {
        $connStr = [System.Environment]::GetEnvironmentVariable("SMARTLOG_DB_CONNECTION", "User")
    }
    if (-not $connStr) {
        $appSettingsPath = Join-Path $Script:InstallDir "appsettings.json"
        if (Test-Path $appSettingsPath) {
            try {
                $appSettings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
                $connStr = $appSettings.ConnectionStrings.DefaultConnection
            }
            catch { }
        }
    }

    if ($connStr) {
        # Parse connection string components
        $dbServer = if ($connStr -match 'Server=([^;]+)')  { $Matches[1] } else { "localhost" }
        $dbName   = if ($connStr -match 'Database=([^;]+)') { $Matches[1] } else { "SmartLog" }
        $dbUser   = if ($connStr -match 'User Id=([^;]+)') { $Matches[1] } else { $null }
        $dbPass   = if ($connStr -match 'Password=([^;]+)') { $Matches[1] } else { $null }

        $dbBackupDir = Join-Path $Script:BackupDir "database"
        if (-not (Test-Path $dbBackupDir)) {
            New-Item -ItemType Directory -Path $dbBackupDir -Force | Out-Null
        }

        $dbBackupFile = Join-Path $dbBackupDir "smartlog-db-$timestamp.bak"
        $backupSql = "BACKUP DATABASE [$dbName] TO DISK = N'$dbBackupFile' WITH NOFORMAT, NOINIT, NAME = N'SmartLog-Full-$timestamp', SKIP, NOREWIND, NOUNLOAD, STATS = 10"

        try {
            $prevEAP = $ErrorActionPreference
            $ErrorActionPreference = "Continue"

            if ($dbUser -and $dbPass) {
                sqlcmd -S $dbServer -U $dbUser -P $dbPass -Q $backupSql 2>&1 | Out-Null
            }
            else {
                sqlcmd -S $dbServer -E -Q $backupSql 2>&1 | Out-Null
            }

            $ErrorActionPreference = $prevEAP

            if ($LASTEXITCODE -eq 0 -and (Test-Path $dbBackupFile)) {
                $dbSize = [math]::Round((Get-Item $dbBackupFile).Length / 1MB, 1)
                Write-Success "Database backed up ($($dbSize) MB) → $dbBackupFile"

                # Keep last 5 database backups
                # @() forces an array so .Count is always available even when only one item is returned
                $oldDbBackups = @(Get-ChildItem $dbBackupDir -Filter "*.bak" | Sort-Object Name -Descending | Select-Object -Skip 5)
                if ($oldDbBackups.Count -gt 0) {
                    $oldDbBackups | Remove-Item -Force
                    Write-Detail "Cleaned up $($oldDbBackups.Count) old DB backup(s)"
                }
            }
            else {
                Write-Warn "Database backup may have failed (exit code: $LASTEXITCODE)"
                Write-Detail "Update will continue — check SQL Server logs if needed."
            }
        }
        catch {
            $ErrorActionPreference = $prevEAP
            Write-Warn "Database backup failed: $_"
            Write-Detail "Update will continue, but no DB backup was created."
        }
    }
    else {
        Write-Warn "Connection string not found — database backup skipped."
        Write-Detail "Set the SMARTLOG_DB_CONNECTION machine environment variable to enable automatic DB backups."
    }
}

# ============================================================
# Step 4: Stop Service & Pull Updates
# ============================================================
Write-StepHeader -Step 4 -Total $totalSteps -Title "Stopping Service & Pulling Updates"

# Stop the service
if ($service.Status -eq 'Running') {
    Write-Detail "Stopping service..."
    Stop-Service -Name $Script:ServiceName -Force
    # Wait for the process to fully exit before overwriting binaries — a 3-second
    # sleep is not enough when the app is mid-shutdown; file locks cause a partial
    # publish that then fails to start.
    $killWait = 0
    while ((Get-Process -Name "SmartLog.Web" -ErrorAction SilentlyContinue) -and $killWait -lt 30) {
        Start-Sleep -Seconds 1
        $killWait++
    }
    if (Get-Process -Name "SmartLog.Web" -ErrorAction SilentlyContinue) {
        Write-Warn "Process still alive after 30s — force-killing..."
        Get-Process -Name "SmartLog.Web" -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Seconds 2
    }
    Write-Success "Service stopped"
}
else {
    Write-Detail "Service was already stopped"
}

# Pull latest code
Write-Detail "Pulling latest changes from '$Branch'..."
$prevEAP = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$pullOutput = git pull origin $Branch 2>&1
$pullExitCode = $LASTEXITCODE
$ErrorActionPreference = $prevEAP
if ($pullExitCode -ne 0) {
    Write-Fail "Git pull failed:"
    Write-Host $pullOutput -ForegroundColor Red
    Write-Host ""
    Write-Warn "Starting service back up..."
    try { Start-Service -Name $Script:ServiceName -ErrorAction Stop } catch { Write-Warn "Could not restart service: $_" }
    Pop-Location
    Read-Host "  Press Enter to exit"
    exit 1
}
Write-Success "Code updated"

# ============================================================
# Step 5: Build & Publish
# ============================================================
Write-StepHeader -Step 5 -Total $totalSteps -Title "Building & Publishing"

$projectDir = Join-Path $repoRoot "src\SmartLog.Web"
Write-Detail "Building in Release mode..."

$publishOutput = dotnet publish $projectDir -c Release -o $Script:InstallDir --nologo 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Fail "Build failed!"
    Write-Host $publishOutput -ForegroundColor Red
    Write-Host ""
    Write-Warn "Restoring from backup and restarting service..."
    if (-not $SkipBackup -and (Test-Path $backupPath)) {
        Copy-Item "$backupPath\*" $Script:InstallDir -Recurse -Force
        Write-Detail "Previous version restored from backup"
    }
    try { Start-Service -Name $Script:ServiceName -ErrorAction Stop } catch { Write-Warn "Could not restart service: $_" }
    Pop-Location
    Read-Host "  Press Enter to exit"
    exit 1
}
Write-Success "Application published to $($Script:InstallDir)"

# ============================================================
# Step 6: Start Service & Verify
# ============================================================
Write-StepHeader -Step 6 -Total $totalSteps -Title "Starting Service & Verifying"

# Determine HTTP / HTTPS ports.
# When SMARTLOG_CERT_PATH + SMARTLOG_CERT_PASSWORD are set, Program.cs uses explicit
# Kestrel config on ports 5050 (HTTP) and 5051 (HTTPS), ignoring ASPNETCORE_URLS.
# Without a cert it falls back to ASPNETCORE_URLS (default 8080).
$Script:CertPath     = [System.Environment]::GetEnvironmentVariable("SMARTLOG_CERT_PATH",     "Machine")
$Script:CertPassword = [System.Environment]::GetEnvironmentVariable("SMARTLOG_CERT_PASSWORD", "Machine")
$httpsEnabled        = $Script:CertPath -and $Script:CertPassword -and (Test-Path $Script:CertPath)

if ($httpsEnabled) {
    $httpPort  = 5050
    $httpsPort = 5051
    $storedHttpsPort = [System.Environment]::GetEnvironmentVariable("SMARTLOG_HTTPS_PORT", "Machine")
    if ($storedHttpsPort) { $httpsPort = [int]$storedHttpsPort }
    Write-Detail "HTTPS enabled — HTTP :$httpPort  HTTPS :$httpsPort"
}
else {
    $httpPort = 8080
    $aspnetUrls = [System.Environment]::GetEnvironmentVariable("ASPNETCORE_URLS", "Machine")
    if ($aspnetUrls -match 'http://[^;]*:(\d+)') {
        $httpPort = [int]$Matches[1]
    }
    Write-Detail "HTTP only — port :$httpPort"
}

Write-Detail "Starting service..."
$serviceStarted = $false
try {
    Start-Service -Name $Script:ServiceName -ErrorAction Stop
    $serviceStarted = $true
}
catch {
    Write-Fail "Service failed to start: $_"
    Write-Host ""
    Write-Host "  Diagnostics" -ForegroundColor Yellow
    Write-Host "  $('-' * 50)" -ForegroundColor DarkGray

    # Show current service status
    try {
        $svc = Get-Service -Name $Script:ServiceName -ErrorAction SilentlyContinue
        if ($svc) { Write-Detail "Service status: $($svc.Status), StartType: $($svc.StartType)" }
    } catch { }

    # Show service binary path (helps spot missing exe / wrong path)
    try {
        $wmi = Get-CimInstance -ClassName Win32_Service -Filter "Name='$($Script:ServiceName)'" -ErrorAction SilentlyContinue
        if ($wmi) {
            Write-Detail "PathName: $($wmi.PathName)"
            Write-Detail "StartName (account): $($wmi.StartName)"
        }
    } catch { }

    # Recent Windows Application Event Log entries for this service
    Write-Host ""
    Write-Detail "Recent Application event log entries:"
    try {
        $events = Get-WinEvent -FilterHashtable @{
            LogName      = 'Application'
            ProviderName = $Script:ServiceName
            StartTime    = (Get-Date).AddMinutes(-10)
        } -MaxEvents 5 -ErrorAction SilentlyContinue
        if (-not $events) {
            $events = Get-WinEvent -FilterHashtable @{
                LogName   = 'System'
                Id        = 7000, 7001, 7009, 7011, 7023, 7024, 7031, 7034
                StartTime = (Get-Date).AddMinutes(-10)
            } -MaxEvents 5 -ErrorAction SilentlyContinue | Where-Object { $_.Message -match $Script:ServiceName }
        }
        if ($events) {
            foreach ($e in $events) {
                $msg = $e.Message
                if ($msg.Length -gt 400) { $msg = $msg.Substring(0, 400) + "..." }
                Write-Host "    [$($e.TimeCreated)] $msg" -ForegroundColor Red
            }
        }
        else {
            Write-Detail "(no recent events found for '$($Script:ServiceName)')"
        }
    } catch {
        Write-Detail "Could not read event log: $_"
    }

    # Tail the most recent app log file
    Write-Host ""
    if (Test-Path $Script:LogDir) {
        $latestLog = Get-ChildItem $Script:LogDir -Filter "*.log" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($latestLog) {
            Write-Detail "Last 20 lines of $($latestLog.Name):"
            Get-Content $latestLog.FullName -Tail 20 -ErrorAction SilentlyContinue | ForEach-Object {
                Write-Host "    $_" -ForegroundColor Gray
            }
        }
        else {
            Write-Detail "No log files found in $($Script:LogDir) — app may have failed before logging started."
        }
    }

    Write-Host ""
    Write-Host "  Common causes" -ForegroundColor Yellow
    Write-Host "  $('-' * 50)" -ForegroundColor DarkGray
    Write-Detail "- Missing/invalid SMARTLOG_DB_CONNECTION (Machine env var)"
    Write-Detail "- Missing SMARTLOG_HMAC_SECRET_KEY"
    Write-Detail "- SQL Server unreachable or DB credentials wrong"
    Write-Detail "- Port already in use (HTTP :$httpPort$(if ($httpsEnabled) { `", HTTPS :$httpsPort`" }))"
    Write-Detail "- Service account lacks permission on $($Script:InstallDir)"
    Write-Detail "- Migrations failed on startup — review the log tail above"
    if ($httpsEnabled) {
        Write-Detail "- Certificate missing or wrong password: $($Script:CertPath)"
    }
    Write-Host ""
    Pop-Location
    Read-Host "  Press Enter to exit"
    exit 1
}

# Poll until Running (up to 60 s) — DB migrations on startup can take >5s
Write-Detail "Waiting for service to reach Running state..."
$pollSecs = 0
do {
    Start-Sleep -Seconds 2
    $pollSecs += 2
    $service = Get-Service -Name $Script:ServiceName
} while ($service.Status -notin @('Running', 'Stopped') -and $pollSecs -lt 60)

if ($service.Status -eq 'Running') {
    Write-Success "Service is running"
}
else {
    Write-Warn "Service status: $($service.Status) (after ${pollSecs}s)"
    Write-Detail "Check logs at: $($Script:LogDir)"
}

# Health check — HTTP
Write-Detail "Checking HTTP response on port $httpPort..."
Start-Sleep -Seconds 3
try {
    $response = Invoke-WebRequest -Uri "http://localhost:$httpPort" -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
    Write-Success "HTTP responding ($($response.StatusCode)) on port $httpPort"
}
catch {
    Write-Warn "HTTP check on port $httpPort failed — app may still be starting"
    Write-Detail "Try: http://localhost:$httpPort"
    Write-Detail "Check logs at: $($Script:LogDir)"
}

# Health check — HTTPS
if ($httpsEnabled) {
    Write-Detail "Checking HTTPS response on port $httpsPort..."
    try {
        # Bypass self-signed certificate validation for this health check
        if ($PSVersionTable.PSVersion.Major -ge 6) {
            $response = Invoke-WebRequest -Uri "https://localhost:$httpsPort" -UseBasicParsing `
                -TimeoutSec 10 -SkipCertificateCheck -ErrorAction Stop
        }
        else {
            [Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
            try {
                $response = Invoke-WebRequest -Uri "https://localhost:$httpsPort" -UseBasicParsing `
                    -TimeoutSec 10 -ErrorAction Stop
            }
            finally {
                [Net.ServicePointManager]::ServerCertificateValidationCallback = $null
            }
        }
        Write-Success "HTTPS responding ($($response.StatusCode)) on port $httpsPort"
    }
    catch {
        Write-Warn "HTTPS check on port $httpsPort failed — self-signed cert may cause browser warning"
        Write-Detail "Try in a browser: https://localhost:$httpsPort"
    }
}

Pop-Location

# ============================================================
# Summary
# ============================================================
$prevEAP = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$currentHash = (git -C $repoRoot rev-parse --short HEAD 2>&1) | Select-Object -Last 1
$ErrorActionPreference = $prevEAP

Write-Host ""
Write-Host "  ======================================================" -ForegroundColor Green
Write-Host "       Update Complete!                                  " -ForegroundColor Green
Write-Host "  ======================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Version:  $currentHash" -ForegroundColor White
Write-Host "  Service:  $($service.Status)" -ForegroundColor White
Write-Host "  HTTP:     http://localhost:$httpPort" -ForegroundColor White
if ($httpsEnabled) {
    Write-Host "  HTTPS:    https://localhost:$httpsPort" -ForegroundColor White
}
Write-Host ""
Write-Host "  Useful commands:" -ForegroundColor Gray
Write-Host "    sc.exe query SmartLogWeb                      Check service status" -ForegroundColor DarkGray
Write-Host "    sc.exe stop SmartLogWeb                       Stop service" -ForegroundColor DarkGray
Write-Host "    sc.exe start SmartLogWeb                      Start service" -ForegroundColor DarkGray
Write-Host "    Get-Content C:\SmartLog\logs\*.log -Tail 50   View logs" -ForegroundColor DarkGray
if ($httpsEnabled) {
    Write-Host "    [Net.ServicePointManager]::ServerCertificateValidationCallback = { `$true }" -ForegroundColor DarkGray
    Write-Host "    (Invoke-WebRequest https://localhost:$httpsPort/health).StatusCode    HTTPS test" -ForegroundColor DarkGray
}
Write-Host ""

if (-not $SkipBackup) {
    Write-Host "  Rollback (if needed):" -ForegroundColor Gray
    Write-Host "    sc.exe stop SmartLogWeb" -ForegroundColor DarkGray
    Write-Host "    Copy-Item '$backupPath\*' '$($Script:InstallDir)' -Recurse -Force" -ForegroundColor DarkGray
    Write-Host "    sc.exe start SmartLogWeb" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  DB Rollback (if needed):" -ForegroundColor Gray
    $dbBackupDir = Join-Path $Script:BackupDir "database"
    $dbBackupFile = Join-Path $dbBackupDir "smartlog-db-$timestamp.bak"
    if (Test-Path $dbBackupFile) {
        Write-Host "    sqlcmd -S <server> -U <user> -P <pass> -Q `"RESTORE DATABASE [SmartLog] FROM DISK = N'$dbBackupFile' WITH REPLACE`"" -ForegroundColor DarkGray
    }
    Write-Host ""
}

Read-Host "  Press Enter to exit"
