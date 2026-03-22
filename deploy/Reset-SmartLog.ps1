#Requires -RunAsAdministrator
<#
.SYNOPSIS
    SmartLog Web App — Reset Installation

.DESCRIPTION
    Removes the SmartLog installation so you can run Setup-SmartLog.ps1 fresh:
    - Stops and removes the Windows Service
    - Removes environment variables
    - Removes firewall rules
    - Optionally deletes the install directory (C:\SmartLog)
    - Optionally drops the database

    Does NOT delete the source code repository or backups.

.NOTES
    Run as Administrator: .\Reset-SmartLog.ps1
    Or use: Reset-SmartLog.bat
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ============================================================
# Configuration
# ============================================================
$Script:ServiceName = "SmartLogWeb"
$Script:InstallDir  = "C:\SmartLog"
$Script:BackupDir   = "C:\SmartLogBackups"

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

function Write-Detail {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor Gray
}

function Read-YesNo {
    param([string]$Prompt, [bool]$Default = $false)
    $defaultText = if ($Default) { "Y/n" } else { "y/N" }
    $result = Read-Host -Prompt "  $Prompt ($defaultText)"
    if ([string]::IsNullOrWhiteSpace($result)) { return $Default }
    return $result -match '^[Yy]'
}

# ============================================================
# Banner
# ============================================================
Clear-Host
Write-Host ""
Write-Host "  ======================================================" -ForegroundColor Red
Write-Host "       SmartLog Web App -- Reset Installation            " -ForegroundColor Red
Write-Host "  ======================================================" -ForegroundColor Red
Write-Host ""
Write-Host "  This will remove the SmartLog installation so you can" -ForegroundColor Gray
Write-Host "  run Setup-SmartLog.ps1 again for a fresh install." -ForegroundColor Gray
Write-Host ""
Write-Host "  The following will be removed:" -ForegroundColor Yellow
Write-Host "    - Windows Service (SmartLogWeb)" -ForegroundColor Yellow
Write-Host "    - Environment variables (SMARTLOG_*, ASPNETCORE_*)" -ForegroundColor Yellow
Write-Host "    - Firewall rules (SmartLog HTTP/HTTPS)" -ForegroundColor Yellow
Write-Host "    - Install directory (C:\SmartLog) [optional]" -ForegroundColor Yellow
Write-Host "    - Database [optional]" -ForegroundColor Yellow
Write-Host ""
Write-Host "  NOT removed: source code repository, backups" -ForegroundColor Gray
Write-Host ""

if (-not (Read-YesNo "Are you sure you want to reset the installation?" $false)) {
    Write-Host ""
    Write-Host "  Reset cancelled." -ForegroundColor Gray
    Read-Host "  Press Enter to exit"
    exit 0
}

$totalSteps = 5

# ============================================================
# Step 1: Stop and Remove Windows Service
# ============================================================
Write-StepHeader -Step 1 -Total $totalSteps -Title "Removing Windows Service"

$service = Get-Service -Name $Script:ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -eq 'Running') {
        Write-Detail "Stopping service..."
        Stop-Service -Name $Script:ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
        Write-Success "Service stopped"
    }

    Write-Detail "Removing service..."
    sc.exe delete $Script:ServiceName | Out-Null
    Start-Sleep -Seconds 2
    Write-Success "Service '$($Script:ServiceName)' removed"
}
else {
    Write-Detail "Service not found, skipping"
}

# ============================================================
# Step 2: Remove Environment Variables
# ============================================================
Write-StepHeader -Step 2 -Total $totalSteps -Title "Removing Environment Variables"

$envVars = @(
    "SMARTLOG_DB_CONNECTION",
    "SMARTLOG_HMAC_SECRET_KEY",
    "SMARTLOG_SEED_PASSWORD",
    "ASPNETCORE_ENVIRONMENT",
    "ASPNETCORE_URLS"
)

foreach ($var in $envVars) {
    $existing = [System.Environment]::GetEnvironmentVariable($var, [System.EnvironmentVariableTarget]::Machine)
    if ($existing) {
        [System.Environment]::SetEnvironmentVariable($var, $null, [System.EnvironmentVariableTarget]::Machine)
        [System.Environment]::SetEnvironmentVariable($var, $null, [System.EnvironmentVariableTarget]::Process)
        Write-Success "Removed $var"
    }
    else {
        Write-Detail "$var not set, skipping"
    }
}

# ============================================================
# Step 3: Remove Firewall Rules
# ============================================================
Write-StepHeader -Step 3 -Total $totalSteps -Title "Removing Firewall Rules"

$rules = @("SmartLog HTTP", "SmartLog HTTPS")
foreach ($rule in $rules) {
    $existing = Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue
    if ($existing) {
        Remove-NetFirewallRule -DisplayName $rule
        Write-Success "Removed firewall rule: $rule"
    }
    else {
        Write-Detail "Rule '$rule' not found, skipping"
    }
}

# ============================================================
# Step 4: Delete Install Directory
# ============================================================
Write-StepHeader -Step 4 -Total $totalSteps -Title "Install Directory"

if (Test-Path $Script:InstallDir) {
    if (Read-YesNo "Delete install directory ($($Script:InstallDir))?" $false) {
        Remove-Item $Script:InstallDir -Recurse -Force
        Write-Success "Deleted $($Script:InstallDir)"
    }
    else {
        Write-Detail "Keeping $($Script:InstallDir)"
    }
}
else {
    Write-Detail "Install directory not found, skipping"
}

if (Test-Path $Script:BackupDir) {
    if (Read-YesNo "Delete backups directory ($($Script:BackupDir))?" $false) {
        Remove-Item $Script:BackupDir -Recurse -Force
        Write-Success "Deleted $($Script:BackupDir)"
    }
    else {
        Write-Detail "Keeping $($Script:BackupDir)"
    }
}

# ============================================================
# Step 5: Drop Database (Optional)
# ============================================================
Write-StepHeader -Step 5 -Total $totalSteps -Title "Database"

if (Read-YesNo "Drop the SmartLog database? (WARNING: all data will be lost)" $false) {
    $sqlInstance = "localhost\SQLEXPRESS"

    # Try default instance if express not found
    $sqlService = Get-Service -Name 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue
    if (-not $sqlService) {
        $sqlService = Get-Service -Name 'MSSQLSERVER' -ErrorAction SilentlyContinue
        if ($sqlService) { $sqlInstance = "localhost" }
    }

    $dbName = Read-Host -Prompt "  Database name to drop [SmartLogDb]"
    if ([string]::IsNullOrWhiteSpace($dbName)) { $dbName = "SmartLogDb" }

    $dbUser = Read-Host -Prompt "  SQL login to drop [SmartLogUser]"
    if ([string]::IsNullOrWhiteSpace($dbUser)) { $dbUser = "SmartLogUser" }

    try {
        $masterConnStr = "Server=$sqlInstance;Database=master;Trusted_Connection=true;TrustServerCertificate=True;"
        $connection = New-Object System.Data.SqlClient.SqlConnection($masterConnStr)
        $connection.Open()

        # Drop database
        $cmd = $connection.CreateCommand()
        $cmd.CommandText = "IF DB_ID('$dbName') IS NOT NULL BEGIN ALTER DATABASE [$dbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$dbName]; END"
        $cmd.ExecuteNonQuery() | Out-Null
        Write-Success "Database '$dbName' dropped"

        # Drop login
        $cmd2 = $connection.CreateCommand()
        $cmd2.CommandText = "IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = '$dbUser') DROP LOGIN [$dbUser]"
        $cmd2.ExecuteNonQuery() | Out-Null
        Write-Success "SQL login '$dbUser' dropped"

        $connection.Close()
    }
    catch {
        Write-Warn "Could not drop database: $_"
        Write-Detail "You can drop it manually in SSMS"
    }
}
else {
    Write-Detail "Keeping database"
}

# ============================================================
# Summary
# ============================================================
Write-Host ""
Write-Host "  ======================================================" -ForegroundColor Green
Write-Host "       Reset Complete!                                   " -ForegroundColor Green
Write-Host "  ======================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  You can now run a fresh installation:" -ForegroundColor Gray
Write-Host "    Right-click Setup-SmartLog.bat -> Run as administrator" -ForegroundColor White
Write-Host ""

Read-Host "  Press Enter to exit"
