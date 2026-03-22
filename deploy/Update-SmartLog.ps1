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
# Step 3: Backup Current Installation
# ============================================================
Write-StepHeader -Step 3 -Total $totalSteps -Title "Backing Up Current Installation"

if ($SkipBackup) {
    Write-Detail "Backup skipped (--SkipBackup flag)"
}
else {
    if (-not (Test-Path $Script:BackupDir)) {
        New-Item -ItemType Directory -Path $Script:BackupDir -Force | Out-Null
    }

    $timestamp = Get-Date -Format "yyyy-MM-dd_HHmmss"
    $backupPath = Join-Path $Script:BackupDir "smartlog-backup-$timestamp"

    Write-Detail "Creating backup at: $backupPath"

    # Copy current installation (exclude logs to save space)
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
    Get-ChildItem $Script:InstallDir -Exclude "logs" | Copy-Item -Destination $backupPath -Recurse -Force

    $backupSize = [math]::Round((Get-ChildItem $backupPath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
    Write-Success "Backup created ($($backupSize) MB)"

    # Clean up old backups (keep last 5)
    $oldBackups = Get-ChildItem $Script:BackupDir -Directory | Sort-Object Name -Descending | Select-Object -Skip 5
    if ($oldBackups) {
        $oldBackups | Remove-Item -Recurse -Force
        Write-Detail "Cleaned up $($oldBackups.Count) old backup(s)"
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
    Start-Sleep -Seconds 3
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
    Start-Service -Name $Script:ServiceName
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
    Start-Service -Name $Script:ServiceName
    Pop-Location
    Read-Host "  Press Enter to exit"
    exit 1
}
Write-Success "Application published to $($Script:InstallDir)"

# ============================================================
# Step 6: Start Service & Verify
# ============================================================
Write-StepHeader -Step 6 -Total $totalSteps -Title "Starting Service & Verifying"

Write-Detail "Starting service..."
Start-Service -Name $Script:ServiceName
Start-Sleep -Seconds 5

$service = Get-Service -Name $Script:ServiceName
if ($service.Status -eq 'Running') {
    Write-Success "Service is running"
}
else {
    Write-Warn "Service status: $($service.Status)"
    Write-Detail "Check logs at: $($Script:LogDir)"
}

# Verify HTTP response
$httpPort = 8080
$aspnetUrls = [System.Environment]::GetEnvironmentVariable("ASPNETCORE_URLS", "Machine")
if ($aspnetUrls -match 'http://.*:(\d+)') {
    $httpPort = $Matches[1]
}

Write-Detail "Checking HTTP response on port $httpPort..."
Start-Sleep -Seconds 3
try {
    $response = Invoke-WebRequest -Uri "http://localhost:$httpPort" -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
    Write-Success "Application responding (HTTP $($response.StatusCode))"
}
catch {
    Write-Warn "HTTP check failed -- app may still be starting"
    Write-Detail "Try accessing http://localhost:$httpPort in a few seconds"
    Write-Detail "Check logs at: $($Script:LogDir)"
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
Write-Host "  URL:      http://localhost:$httpPort" -ForegroundColor White
Write-Host ""
Write-Host "  Useful commands:" -ForegroundColor Gray
Write-Host "    sc.exe query SmartLogWeb        Check service status" -ForegroundColor DarkGray
Write-Host "    sc.exe stop SmartLogWeb         Stop service" -ForegroundColor DarkGray
Write-Host "    sc.exe start SmartLogWeb        Start service" -ForegroundColor DarkGray
Write-Host "    Get-Content C:\SmartLog\logs\*.log -Tail 50   View logs" -ForegroundColor DarkGray
Write-Host ""

if (-not $SkipBackup) {
    Write-Host "  Rollback (if needed):" -ForegroundColor Gray
    Write-Host "    sc.exe stop SmartLogWeb" -ForegroundColor DarkGray
    Write-Host "    Copy-Item '$backupPath\*' '$($Script:InstallDir)' -Recurse -Force" -ForegroundColor DarkGray
    Write-Host "    sc.exe start SmartLogWeb" -ForegroundColor DarkGray
    Write-Host ""
}

Read-Host "  Press Enter to exit"
