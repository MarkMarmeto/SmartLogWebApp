#Requires -RunAsAdministrator
<#
.SYNOPSIS
    SmartLog — Automated Backup Script

.DESCRIPTION
    Backs up the SmartLog database and application files.
    Designed to be run as a Windows Scheduled Task (daily).

    Backups are stored in C:\SmartLogBackups\
      - database\  : SQL Server .bak files
      - app\       : Application file archives

    Retention: keeps the last 7 daily backups of each type.

.NOTES
    Usage (manual):
      .\Backup-SmartLog.ps1

    To register as a daily scheduled task:
      .\Backup-SmartLog.ps1 -Register

    To unregister the scheduled task:
      .\Backup-SmartLog.ps1 -Unregister
#>

[CmdletBinding()]
param(
    [switch]$Register,
    [switch]$Unregister,
    [int]$RetainDays = 7
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Configuration ────────────────────────────────────────────
$Script:ServiceName = "SmartLogWeb"
$Script:InstallDir  = "C:\SmartLog"
$Script:BackupDir   = "C:\SmartLogBackups"
$Script:TaskName    = "SmartLog Daily Backup"
$Script:LogFile     = "C:\SmartLogBackups\backup.log"

# ── Helpers ──────────────────────────────────────────────────
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "[$ts] [$Level] $Message"
    Write-Host $line -ForegroundColor $(if ($Level -eq "ERROR") { "Red" } elseif ($Level -eq "WARN") { "Yellow" } else { "Cyan" })
    Add-Content -Path $Script:LogFile -Value $line -ErrorAction SilentlyContinue
}

function Write-Success { param([string]$m) Write-Host "  [OK] $m" -ForegroundColor Green }
function Write-Warn    { param([string]$m) Write-Host "  [!!] $m" -ForegroundColor Yellow }
function Write-Fail    { param([string]$m) Write-Host "  [XX] $m" -ForegroundColor Red }

# ── Register Scheduled Task ───────────────────────────────────
if ($Register) {
    Write-Host ""
    Write-Host "  Registering SmartLog Daily Backup scheduled task..." -ForegroundColor Cyan

    $scriptPath = $PSCommandPath
    $action  = New-ScheduledTaskAction -Execute "powershell.exe" `
                   -Argument "-NonInteractive -ExecutionPolicy Bypass -File `"$scriptPath`""
    $trigger = New-ScheduledTaskTrigger -Daily -At "02:00AM"
    $settings = New-ScheduledTaskSettingsSet `
                   -ExecutionTimeLimit (New-TimeSpan -Hours 1) `
                   -StartWhenAvailable `
                   -RunOnlyIfNetworkAvailable:$false

    $principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -RunLevel Highest

    Register-ScheduledTask `
        -TaskName   $Script:TaskName `
        -Action     $action `
        -Trigger    $trigger `
        -Settings   $settings `
        -Principal  $principal `
        -Description "Daily backup of SmartLog database and application files" `
        -Force | Out-Null

    Write-Success "Scheduled task '$($Script:TaskName)' registered — runs daily at 2:00 AM."
    Write-Host ""
    exit 0
}

# ── Unregister Scheduled Task ─────────────────────────────────
if ($Unregister) {
    Unregister-ScheduledTask -TaskName $Script:TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Success "Scheduled task '$($Script:TaskName)' removed."
    exit 0
}

# ── Run Backup ────────────────────────────────────────────────
$Timestamp  = Get-Date -Format "yyyy-MM-dd_HH-mm"
$DbDir      = Join-Path $Script:BackupDir "database"
$AppDir     = Join-Path $Script:BackupDir "app"

Write-Host ""
Write-Host "  SmartLog Backup — $Timestamp" -ForegroundColor Cyan
Write-Host "  $('=' * 50)" -ForegroundColor DarkGray

# Ensure backup directories exist
New-Item -ItemType Directory -Force -Path $DbDir  | Out-Null
New-Item -ItemType Directory -Force -Path $AppDir | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path $Script:LogFile) | Out-Null

Write-Log "Backup started — $Timestamp"

# ── 1. Database Backup ────────────────────────────────────────
Write-Host ""
Write-Host "  [1/2] Database Backup" -ForegroundColor Cyan
Write-Host "  $('─' * 50)" -ForegroundColor DarkGray

$DbBakFile = Join-Path $DbDir "smartlog-db-$Timestamp.bak"

# Resolve connection string
$ConnectionString = $env:SMARTLOG_DB_CONNECTION
if (-not $ConnectionString) {
    $AppSettings = Join-Path $Script:InstallDir "appsettings.json"
    if (Test-Path $AppSettings) {
        try {
            $json = Get-Content $AppSettings -Raw | ConvertFrom-Json
            $ConnectionString = $json.ConnectionStrings.DefaultConnection
        } catch {
            Write-Warn "Could not read appsettings.json for connection string"
        }
    }
}

if ($ConnectionString) {
    # Extract server and database from connection string
    $Server   = [regex]::Match($ConnectionString, '(?i)Server=([^;]+)').Groups[1].Value.Trim()
    $Database = [regex]::Match($ConnectionString, '(?i)(?:Database|Initial Catalog)=([^;]+)').Groups[1].Value.Trim()

    if ($Server -and $Database) {
        try {
            $SqlCmd = "BACKUP DATABASE [$Database] TO DISK = N'$DbBakFile' WITH FORMAT, COMPRESSION, STATS = 10"
            $result = sqlcmd -S $Server -E -Q $SqlCmd 2>&1

            if ($LASTEXITCODE -eq 0) {
                $sizeMb = [math]::Round((Get-Item $DbBakFile).Length / 1MB, 1)
                Write-Success "Database backed up → $DbBakFile ($sizeMb MB)"
                Write-Log "Database backup OK — $DbBakFile ($sizeMb MB)"
            } else {
                Write-Warn "sqlcmd exited with code $LASTEXITCODE. Output: $result"
                Write-Log "Database backup warning — exit $LASTEXITCODE" "WARN"
            }
        } catch {
            Write-Fail "Database backup failed: $_"
            Write-Log "Database backup failed: $_" "ERROR"
        }

        # Prune old database backups
        Get-ChildItem -Path $DbDir -Filter "*.bak" |
            Sort-Object LastWriteTime -Descending |
            Select-Object -Skip $RetainDays |
            ForEach-Object { Remove-Item $_.FullName -Force; Write-Log "Pruned old DB backup: $($_.Name)" }
    } else {
        Write-Warn "Could not parse Server or Database from connection string — skipping DB backup"
        Write-Log "DB backup skipped — could not parse connection string" "WARN"
    }
} else {
    Write-Warn "No connection string found (set SMARTLOG_DB_CONNECTION or check appsettings.json)"
    Write-Log "DB backup skipped — no connection string" "WARN"
}

# ── 2. Application Files Backup ───────────────────────────────
Write-Host ""
Write-Host "  [2/2] Application Files Backup" -ForegroundColor Cyan
Write-Host "  $('─' * 50)" -ForegroundColor DarkGray

if (Test-Path $Script:InstallDir) {
    $AppZip = Join-Path $AppDir "smartlog-app-$Timestamp.zip"
    try {
        Compress-Archive -Path "$($Script:InstallDir)\*" `
                         -DestinationPath $AppZip `
                         -CompressionLevel Optimal `
                         -Exclude @("logs\*", "*.log") `
                         -Force
        $sizeMb = [math]::Round((Get-Item $AppZip).Length / 1MB, 1)
        Write-Success "App files backed up → $AppZip ($sizeMb MB)"
        Write-Log "App backup OK — $AppZip ($sizeMb MB)"
    } catch {
        Write-Fail "App backup failed: $_"
        Write-Log "App backup failed: $_" "ERROR"
    }

    # Prune old app backups
    Get-ChildItem -Path $AppDir -Filter "*.zip" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -Skip $RetainDays |
        ForEach-Object { Remove-Item $_.FullName -Force; Write-Log "Pruned old app backup: $($_.Name)" }
} else {
    Write-Warn "Install directory not found: $($Script:InstallDir) — skipping app backup"
    Write-Log "App backup skipped — install dir not found" "WARN"
}

# ── Done ──────────────────────────────────────────────────────
Write-Host ""
Write-Host "  Backup complete." -ForegroundColor Green
Write-Log "Backup complete"
Write-Host ""
