#Requires -RunAsAdministrator
<#
.SYNOPSIS
    SmartLog Web App -- Automated Setup Wizard for Windows

.DESCRIPTION
    Interactive setup script that automates the full installation of SmartLog Web App:
    - Checks prerequisites (.NET 8 SDK, SQL Server)
    - Creates the database and SQL login
    - Generates secure HMAC secret key
    - Sets system environment variables
    - Optionally generates a self-signed TLS certificate and enables HTTPS
    - Builds and publishes the application
    - Configures firewall rules
    - Optionally registers as a Windows Service

    All secrets are entered interactively or generated securely.
    No secrets are written to files.

.NOTES
    Run this script as Administrator in PowerShell.
    Usage: .\Setup-SmartLog.ps1
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ============================================================
# Configuration
# ============================================================
$Script:ServiceName     = "SmartLogWeb"
$Script:ServiceDisplay  = "SmartLog Web Application"
$Script:InstallDir      = "C:\SmartLog"
$Script:LogDir          = "C:\SmartLog\logs"
$Script:BackupDir       = "C:\SmartLogBackups"
$Script:HttpPort        = 8080
$Script:HttpsPort       = 5051
$Script:EnableHttps     = $false
$Script:CertPath        = "C:\SmartLog\smartlog.pfx"
$Script:CertPassword    = ""
$Script:CertThumbprint  = ""
$Script:DbName          = "SmartLogDb"
$Script:DbUser          = "SmartLogUser"
$Script:SqlInstance     = "localhost\SQLEXPRESS"

# ============================================================
# Helper Functions
# ============================================================
function Write-StepHeader {
    param([int]$Step, [int]$Total, [string]$Title)
    Write-Host ""
    Write-Host "  [$Step/$Total] $Title" -ForegroundColor Cyan
    Write-Host "  $('─' * 50)" -ForegroundColor DarkGray
}

function Write-Success {
    param([string]$Message)
    Write-Host "  ✓ $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "  ⚠ $Message" -ForegroundColor Yellow
}

function Write-Fail {
    param([string]$Message)
    Write-Host "  ✗ $Message" -ForegroundColor Red
}

function Write-Detail {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor Gray
}

function Read-SecureInput {
    param([string]$Prompt)
    $secure = Read-Host -Prompt "  $Prompt" -AsSecureString
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        return [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Read-Input {
    param([string]$Prompt, [string]$Default = "")
    if ($Default) {
        $result = Read-Host -Prompt "  $Prompt [$Default]"
        if ([string]::IsNullOrWhiteSpace($result)) { return $Default }
        return $result
    }
    return Read-Host -Prompt "  $Prompt"
}

function Read-YesNo {
    param([string]$Prompt, [bool]$Default = $true)
    $defaultText = if ($Default) { "Y/n" } else { "y/N" }
    $result = Read-Host -Prompt "  $Prompt ($defaultText)"
    if ([string]::IsNullOrWhiteSpace($result)) { return $Default }
    return $result -match '^[Yy]'
}

function Generate-SecureKey {
    param([int]$Bytes = 32)
    $keyBytes = New-Object byte[] $Bytes
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($keyBytes)
    $rng.Dispose()
    return [Convert]::ToBase64String($keyBytes)
}

function Generate-SecurePassword {
    param([int]$Length = 20)
    $chars = 'abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$%&*'
    $password = ""
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 1

    # Ensure at least one of each required type
    $password += ('ABCDEFGHJKLMNPQRSTUVWXYZ'[(Get-SecureRandomInt -Max 24 -Rng $rng)])
    $password += ('abcdefghijkmnopqrstuvwxyz'[(Get-SecureRandomInt -Max 25 -Rng $rng)])
    $password += ('23456789'[(Get-SecureRandomInt -Max 8 -Rng $rng)])
    $password += ('!@#$%&*'[(Get-SecureRandomInt -Max 7 -Rng $rng)])

    for ($i = $password.Length; $i -lt $Length; $i++) {
        $password += $chars[(Get-SecureRandomInt -Max $chars.Length -Rng $rng)]
    }

    # Shuffle the password
    $charArray = $password.ToCharArray()
    for ($i = $charArray.Length - 1; $i -gt 0; $i--) {
        $j = Get-SecureRandomInt -Max ($i + 1) -Rng $rng
        $temp = $charArray[$i]
        $charArray[$i] = $charArray[$j]
        $charArray[$j] = $temp
    }

    $rng.Dispose()
    return -join $charArray
}

function Get-SecureRandomInt {
    param([int]$Max, [System.Security.Cryptography.RandomNumberGenerator]$Rng)
    $bytes = New-Object byte[] 4
    $Rng.GetBytes($bytes)
    return [Math]::Abs([BitConverter]::ToInt32($bytes, 0)) % $Max
}

function Test-SqlConnection {
    param([string]$ConnectionString)
    try {
        $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
        $connection.Open()
        $connection.Close()
        return $true
    }
    catch {
        return $false
    }
}

function Invoke-SqlCommand {
    param([string]$ConnectionString, [string]$Query)
    $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    $connection.Open()
    try {
        $command = $connection.CreateCommand()
        $command.CommandText = $Query
        $command.ExecuteNonQuery() | Out-Null
    }
    finally {
        $connection.Close()
    }
}

function Invoke-SqlScalar {
    param([string]$ConnectionString, [string]$Query)
    $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    $connection.Open()
    try {
        $command = $connection.CreateCommand()
        $command.CommandText = $Query
        return $command.ExecuteScalar()
    }
    finally {
        $connection.Close()
    }
}

# ============================================================
# Banner
# ============================================================
Clear-Host
Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "  ║                                                  ║" -ForegroundColor Magenta
Write-Host "  ║       SmartLog Web App -- Setup Wizard           ║" -ForegroundColor Magenta
Write-Host "  ║       Windows Server Installation                ║" -ForegroundColor Magenta
Write-Host "  ║                                                  ║" -ForegroundColor Magenta
Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""
Write-Host "  This wizard will set up SmartLog Web App on this machine." -ForegroundColor Gray
Write-Host "  Make sure you are running PowerShell as Administrator." -ForegroundColor Gray
Write-Host ""

$totalSteps = 10

# ============================================================
# Step 1: Check Prerequisites
# ============================================================
Write-StepHeader -Step 1 -Total $totalSteps -Title "Checking Prerequisites"

# Check .NET SDK
$dotnetVersion = $null
try {
    $dotnetVersion = (dotnet --version 2>$null)
}
catch { }

if ($dotnetVersion -and $dotnetVersion -match '^8\.') {
    Write-Success ".NET SDK $dotnetVersion installed"
}
elseif ($dotnetVersion) {
    Write-Warn ".NET SDK $dotnetVersion found, but 8.0+ is required"
    Write-Host ""
    Write-Host "  Download .NET 8.0 SDK from:" -ForegroundColor Yellow
    Write-Host "  https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Cyan
    Write-Host ""
    if (-not (Read-YesNo "Continue anyway?")) { exit 1 }
}
else {
    Write-Fail ".NET SDK not found"
    Write-Host ""
    Write-Host "  Download .NET 8.0 SDK from:" -ForegroundColor Yellow
    Write-Host "  https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Install it and run this script again." -ForegroundColor Yellow
    Read-Host "  Press Enter to exit"
    exit 1
}

# Check Git
$gitVersion = $null
try {
    $gitVersion = (git --version 2>$null)
}
catch { }

if ($gitVersion) {
    Write-Success "Git installed ($gitVersion)"
}
else {
    Write-Warn "Git not found -- you'll need to copy the source code manually"
}

# Check SQL Server
$sqlRunning = $false
try {
    $sqlService = Get-Service -Name 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue
    if ($sqlService -and $sqlService.Status -eq 'Running') {
        $sqlRunning = $true
        Write-Success "SQL Server Express is running"
    }
    else {
        # Try default instance
        $sqlService = Get-Service -Name 'MSSQLSERVER' -ErrorAction SilentlyContinue
        if ($sqlService -and $sqlService.Status -eq 'Running') {
            $sqlRunning = $true
            $Script:SqlInstance = "localhost"
            Write-Success "SQL Server (default instance) is running"
        }
    }
}
catch { }

if (-not $sqlRunning) {
    Write-Fail "SQL Server is not running or not installed"
    Write-Host ""
    Write-Host "  Download SQL Server Express from:" -ForegroundColor Yellow
    Write-Host "  https://www.microsoft.com/en-us/sql-server/sql-server-downloads" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  After installing, enable TCP/IP in SQL Server Configuration Manager" -ForegroundColor Yellow
    Write-Host "  and restart the SQL Server service." -ForegroundColor Yellow
    Write-Host ""
    if (-not (Read-YesNo "Continue anyway (SQL Server on a different machine)?")) { exit 1 }
    $Script:SqlInstance = Read-Input "Enter SQL Server instance" "localhost\SQLEXPRESS"
}

# ============================================================
# Step 2: Configure SQL Server Instance
# ============================================================
Write-StepHeader -Step 2 -Total $totalSteps -Title "Database Configuration"

$Script:SqlInstance = Read-Input "SQL Server instance" $Script:SqlInstance
$Script:DbName = Read-Input "Database name" $Script:DbName
$Script:DbUser = Read-Input "Database username (will be created)" $Script:DbUser

# Generate or enter database password
if (Read-YesNo "Generate a secure database password automatically?" $true) {
    $dbPassword = Generate-SecurePassword -Length 24
    Write-Success "Database password generated"
}
else {
    $dbPassword = Read-SecureInput "Enter database password"
    if ($dbPassword.Length -lt 8) {
        Write-Fail "Password must be at least 8 characters"
        exit 1
    }
}

# Build connection strings
$masterConnStr = "Server=$($Script:SqlInstance);Database=master;Trusted_Connection=true;TrustServerCertificate=True;"
$appConnStr = "Server=$($Script:SqlInstance);Database=$($Script:DbName);User Id=$($Script:DbUser);Password=$dbPassword;TrustServerCertificate=True;"

# ============================================================
# Step 3: Create Database & Login
# ============================================================
Write-StepHeader -Step 3 -Total $totalSteps -Title "Creating Database & SQL Login"

# Test connection to SQL Server
Write-Detail "Connecting to SQL Server ($($Script:SqlInstance))..."
if (-not (Test-SqlConnection $masterConnStr)) {
    Write-Fail "Cannot connect to SQL Server at $($Script:SqlInstance)"
    Write-Host ""
    Write-Host "  Possible issues:" -ForegroundColor Yellow
    Write-Host "    - SQL Server service not running" -ForegroundColor Gray
    Write-Host "    - TCP/IP not enabled in SQL Server Configuration Manager" -ForegroundColor Gray
    Write-Host "    - Instance name is incorrect" -ForegroundColor Gray
    Write-Host ""
    if (-not (Read-YesNo "Skip database creation (configure manually later)?" $false)) { exit 1 }
}
else {
    Write-Success "Connected to SQL Server"

    # Check if database exists
    $dbExists = Invoke-SqlScalar -ConnectionString $masterConnStr -Query "SELECT name FROM sys.databases WHERE name = '$($Script:DbName)'"
    if ($dbExists -ne $null) {
        Write-Warn "Database '$($Script:DbName)' already exists"
        if (Read-YesNo "Use existing database?" $true) {
            Write-Detail "Using existing database"
        }
        else {
            Write-Fail "Setup cancelled. Rename the existing database or choose a different name."
            exit 1
        }
    }
    else {
        Write-Detail "Creating database '$($Script:DbName)'..."
        Invoke-SqlCommand -ConnectionString $masterConnStr -Query "CREATE DATABASE [$($Script:DbName)];"
        Write-Success "Database '$($Script:DbName)' created"
    }

    # Enable mixed mode authentication
    Write-Detail "Enabling SQL Server & Windows Authentication mode..."
    try {
        Invoke-SqlCommand -ConnectionString $masterConnStr -Query @"
EXEC xp_instance_regwrite
    N'HKEY_LOCAL_MACHINE',
    N'Software\Microsoft\MSSQLServer\MSSQLServer',
    N'LoginMode', REG_DWORD, 2;
"@
        Write-Success "Mixed authentication mode enabled"
        Write-Warn "SQL Server service needs to be restarted for auth mode change"
    }
    catch {
        Write-Warn "Could not change auth mode automatically -- you may need to do this in SSMS"
    }

    # Check if login exists
    $loginExists = Invoke-SqlScalar -ConnectionString $masterConnStr -Query "SELECT 1 FROM sys.server_principals WHERE name = '$($Script:DbUser)'"
    if ($loginExists) {
        Write-Warn "SQL login '$($Script:DbUser)' already exists"
        Write-Detail "Updating password..."
        try {
            Invoke-SqlCommand -ConnectionString $masterConnStr -Query "ALTER LOGIN [$($Script:DbUser)] WITH PASSWORD = '$dbPassword';"
            Write-Success "Password updated for existing login"
        }
        catch {
            Write-Warn "Could not update password -- the existing login will be used"
        }
    }
    else {
        Write-Detail "Creating SQL login '$($Script:DbUser)'..."
        Invoke-SqlCommand -ConnectionString $masterConnStr -Query "CREATE LOGIN [$($Script:DbUser)] WITH PASSWORD = '$dbPassword';"
        Write-Success "SQL login '$($Script:DbUser)' created"
    }

    # Create database user and grant permissions (use master connection to avoid login issues)
    $userExistsQuery = "USE [$($Script:DbName)]; SELECT 1 FROM sys.database_principals WHERE name = '$($Script:DbUser)'"
    $userExists = Invoke-SqlScalar -ConnectionString $masterConnStr -Query $userExistsQuery
    if (-not $userExists) {
        Write-Detail "Creating database user and granting permissions..."
        Invoke-SqlCommand -ConnectionString $masterConnStr -Query @"
USE [$($Script:DbName)];
CREATE USER [$($Script:DbUser)] FOR LOGIN [$($Script:DbUser)];
ALTER ROLE db_owner ADD MEMBER [$($Script:DbUser)];
"@
        Write-Success "Database user created with db_owner role"
    }
    else {
        Write-Detail "Database user already exists, ensuring permissions..."
        Invoke-SqlCommand -ConnectionString $masterConnStr -Query "USE [$($Script:DbName)]; ALTER ROLE db_owner ADD MEMBER [$($Script:DbUser)];"
        Write-Success "Database permissions verified"
    }

    # Restart SQL Server to apply auth mode
    if (Read-YesNo "Restart SQL Server now to apply authentication changes?" $true) {
        $svcName = if ($Script:SqlInstance -match '\\(.+)$') { "MSSQL`$$($Matches[1])" } else { "MSSQLSERVER" }
        Write-Detail "Restarting $svcName..."
        Restart-Service -Name $svcName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
        Write-Success "SQL Server restarted"
    }

    # Verify app connection
    Write-Detail "Verifying application connection string..."
    if (Test-SqlConnection $appConnStr) {
        Write-Success "Application can connect to database"
    }
    else {
        Write-Warn "Application connection test failed -- SQL Server may still be restarting"
        Write-Detail "The connection will be retried when the app starts"
    }
}

# ============================================================
# Step 4: Security Configuration
# ============================================================
Write-StepHeader -Step 4 -Total $totalSteps -Title "Security Configuration"

# HMAC Secret Key
Write-Host ""
Write-Host "  The HMAC secret key is used to sign QR codes." -ForegroundColor Gray
Write-Host "  The same key must be configured in the Scanner App." -ForegroundColor Gray
Write-Host ""

if (Read-YesNo "Generate a new HMAC secret key?" $true) {
    $hmacSecret = Generate-SecureKey -Bytes 32
    Write-Success "HMAC secret key generated"
}
else {
    $hmacSecret = Read-SecureInput "Enter existing HMAC secret key"
    if ([string]::IsNullOrWhiteSpace($hmacSecret)) {
        Write-Fail "HMAC secret key cannot be empty"
        exit 1
    }
    Write-Success "HMAC secret key accepted"
}

# Admin password
Write-Host ""
Write-Host "  Set the password for the default admin accounts." -ForegroundColor Gray
Write-Host "  Requirements: 8+ chars, uppercase, lowercase, digit, special char" -ForegroundColor Gray
Write-Host ""

$adminPassword = Read-SecureInput "Enter admin password"

# Validate password
$pwValid = $true
if ($adminPassword.Length -lt 8) { Write-Fail "Password must be at least 8 characters"; $pwValid = $false }
if ($adminPassword -cnotmatch '[A-Z]') { Write-Fail "Password must contain an uppercase letter"; $pwValid = $false }
if ($adminPassword -cnotmatch '[a-z]') { Write-Fail "Password must contain a lowercase letter"; $pwValid = $false }
if ($adminPassword -notmatch '\d') { Write-Fail "Password must contain a digit"; $pwValid = $false }
if ($adminPassword -notmatch '[^a-zA-Z0-9]') { Write-Fail "Password must contain a special character"; $pwValid = $false }

if (-not $pwValid) {
    Write-Host ""
    if (Read-YesNo "Generate a secure password instead?" $true) {
        $adminPassword = Generate-SecurePassword -Length 16
        Write-Success "Admin password generated"
    }
    else {
        Write-Fail "Invalid password. Please run the script again."
        exit 1
    }
}
else {
    Write-Success "Admin password accepted"
}

# ============================================================
# Step 5: Network Configuration
# ============================================================
Write-StepHeader -Step 5 -Total $totalSteps -Title "Network Configuration"

$Script:HttpPort = [int](Read-Input "HTTP port" "$($Script:HttpPort)")

# Detect current IP
$currentIP = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.InterfaceAlias -notmatch 'Loopback' -and $_.PrefixOrigin -ne 'WellKnown' } | Select-Object -First 1).IPAddress
if ($currentIP) {
    Write-Detail "Detected IP address: $currentIP"
}

Write-Host ""
Write-Host "  Scanner devices will connect to this server using:" -ForegroundColor Gray
Write-Host "  http://$($currentIP):$($Script:HttpPort)" -ForegroundColor White
Write-Host ""

# ============================================================
# Step 6: HTTPS Configuration (Optional)
# ============================================================
Write-StepHeader -Step 6 -Total $totalSteps -Title "HTTPS Configuration (Optional)"

Write-Host ""
Write-Host "  HTTPS encrypts traffic between browsers/scanners and this server." -ForegroundColor Gray
Write-Host "  A self-signed certificate is generated and trusted on this machine." -ForegroundColor Gray
Write-Host "  Browsers show a one-time security warning -- click Advanced -> Proceed." -ForegroundColor Gray
Write-Host ""

if (Read-YesNo "Enable HTTPS?" $false) {
    $Script:EnableHttps = $true
    $Script:HttpsPort = [int](Read-Input "HTTPS port" "$($Script:HttpsPort)")

    Write-Host ""
    $certHostname = if ($currentIP) { $currentIP } else { $env:COMPUTERNAME }

    # Build SAN list
    $sanList = @($certHostname, $env:COMPUTERNAME, "localhost", "127.0.0.1")
    $localIps = (Get-NetIPAddress -AddressFamily IPv4 |
                 Where-Object { $_.InterfaceAlias -notmatch "Loopback" } |
                 Select-Object -ExpandProperty IPAddress)
    $sanList = ($sanList + $localIps) | Select-Object -Unique
    Write-Detail "Certificate SANs: $($sanList -join ', ')"

    # Generate self-signed certificate
    Write-Detail "Generating self-signed certificate (valid 3 years)..."
    $cert = New-SelfSignedCertificate `
        -DnsName $sanList `
        -CertStoreLocation "cert:\LocalMachine\My" `
        -NotAfter (Get-Date).AddDays(1095) `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -KeyUsage DigitalSignature, KeyEncipherment `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1")

    $Script:CertThumbprint = $cert.Thumbprint
    Write-Success "Certificate generated -- thumbprint: $($Script:CertThumbprint)"

    # Trust on this machine
    $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store(
        [System.Security.Cryptography.X509Certificates.StoreName]::Root,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    $rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    $rootStore.Add($cert)
    $rootStore.Close()
    Write-Success "Certificate added to Trusted Root CA store"

    # Export to PFX with auto-generated password
    New-Item -ItemType Directory -Force -Path (Split-Path $Script:CertPath) | Out-Null
    $pfxChars = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$'
    $Script:CertPassword = -join ((1..24) | ForEach-Object { $pfxChars[(Get-Random -Maximum $pfxChars.Length)] })
    $securePfxPw = ConvertTo-SecureString -String $Script:CertPassword -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $Script:CertPath -Password $securePfxPw | Out-Null
    Write-Success "Certificate exported to $($Script:CertPath)"

    Write-Host ""
    Write-Host "  Scanner devices should use HTTPS URL:" -ForegroundColor Gray
    Write-Host "  https://${certHostname}:$($Script:HttpsPort)" -ForegroundColor White
    Write-Host ""
}
else {
    Write-Detail "Skipped -- app will serve HTTP only"
}

# ============================================================
# Step 7: Set Environment Variables
# ============================================================
Write-StepHeader -Step 7 -Total $totalSteps -Title "Setting Environment Variables"

$aspnetUrls = "http://0.0.0.0:$($Script:HttpPort)"
if ($Script:EnableHttps) {
    $aspnetUrls += ";https://0.0.0.0:$($Script:HttpsPort)"
}

$envVars = @{
    "SMARTLOG_DB_CONNECTION"   = $appConnStr
    "SMARTLOG_HMAC_SECRET_KEY" = $hmacSecret
    "SMARTLOG_SEED_PASSWORD"   = $adminPassword
    "ASPNETCORE_ENVIRONMENT"   = "Production"
    "ASPNETCORE_URLS"          = $aspnetUrls
}

if ($Script:EnableHttps) {
    $envVars["SMARTLOG_CERT_PATH"]     = $Script:CertPath
    $envVars["SMARTLOG_CERT_PASSWORD"] = $Script:CertPassword
    $envVars["SMARTLOG_HTTPS_PORT"]    = $Script:HttpsPort.ToString()
}

foreach ($key in $envVars.Keys) {
    [System.Environment]::SetEnvironmentVariable($key, $envVars[$key], [System.EnvironmentVariableTarget]::Machine)
    # Also set for current process
    [System.Environment]::SetEnvironmentVariable($key, $envVars[$key], [System.EnvironmentVariableTarget]::Process)
    $displayValue = if ($key -match 'PASSWORD|SECRET|CONNECTION') { "****" } else { $envVars[$key] }
    Write-Success "$key = $displayValue"
}

Write-Success "All environment variables set (system-wide)"

# ============================================================
# Step 8: Build & Publish
# ============================================================
Write-StepHeader -Step 8 -Total $totalSteps -Title "Building & Publishing Application"

# Find the project root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$projectDir = Join-Path $repoRoot "src\SmartLog.Web"

if (-not (Test-Path (Join-Path $projectDir "SmartLog.Web.csproj"))) {
    Write-Warn "Project not found at $projectDir"
    $repoRoot = Read-Input "Enter the path to SmartLogWebApp repository root"
    $projectDir = Join-Path $repoRoot "src\SmartLog.Web"
    if (-not (Test-Path (Join-Path $projectDir "SmartLog.Web.csproj"))) {
        Write-Fail "SmartLog.Web.csproj not found. Check the path and try again."
        exit 1
    }
}

Write-Detail "Repository: $repoRoot"
Write-Detail "Publishing to: $($Script:InstallDir)"

# Create install directory
if (-not (Test-Path $Script:InstallDir)) {
    New-Item -ItemType Directory -Path $Script:InstallDir -Force | Out-Null
}

# Create log directory
if (-not (Test-Path $Script:LogDir)) {
    New-Item -ItemType Directory -Path $Script:LogDir -Force | Out-Null
}

# Create backup directory
if (-not (Test-Path $Script:BackupDir)) {
    New-Item -ItemType Directory -Path $Script:BackupDir -Force | Out-Null
}

Write-Detail "Building and publishing (this may take a minute)..."
$publishOutput = dotnet publish $projectDir -c Release -o $Script:InstallDir --nologo 2>&1
$publishExitCode = $LASTEXITCODE

if ($publishExitCode -ne 0) {
    Write-Fail "Build failed!"
    Write-Host $publishOutput -ForegroundColor Red
    exit 1
}

Write-Success "Application published to $($Script:InstallDir)"

# ============================================================
# Step 9: Firewall Rules
# ============================================================
Write-StepHeader -Step 9 -Total $totalSteps -Title "Configuring Firewall"

Remove-NetFirewallRule -DisplayName "SmartLog HTTP" -ErrorAction SilentlyContinue
Remove-NetFirewallRule -DisplayName "SmartLog HTTPS" -ErrorAction SilentlyContinue

New-NetFirewallRule -DisplayName "SmartLog HTTP" -Direction Inbound -Protocol TCP -LocalPort $Script:HttpPort -Action Allow -Profile Domain,Private | Out-Null
Write-Success "Firewall rule created: HTTP port $($Script:HttpPort) (Domain, Private networks)"

if ($Script:EnableHttps) {
    New-NetFirewallRule -DisplayName "SmartLog HTTPS" -Direction Inbound -Protocol TCP -LocalPort $Script:HttpsPort -Action Allow -Profile Domain,Private | Out-Null
    Write-Success "Firewall rule created: HTTPS port $($Script:HttpsPort) (Domain, Private networks)"
}

# ============================================================
# Step 10: Install Windows Service
# ============================================================
Write-StepHeader -Step 10 -Total $totalSteps -Title "Windows Service Setup"

if (Read-YesNo "Install SmartLog as a Windows Service (auto-start on boot)?" $true) {

    # Stop and remove existing service
    $existingService = Get-Service -Name $Script:ServiceName -ErrorAction SilentlyContinue
    if ($existingService) {
        Write-Detail "Stopping existing service..."
        Stop-Service -Name $Script:ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Detail "Removing existing service..."
        sc.exe delete $Script:ServiceName | Out-Null
        Start-Sleep -Seconds 2
    }

    # Create service
    $exePath = Join-Path $Script:InstallDir "SmartLog.Web.exe"
    sc.exe create $Script:ServiceName binPath="$exePath" DisplayName="$($Script:ServiceDisplay)" start=auto obj=LocalSystem | Out-Null
    sc.exe description $Script:ServiceName "SmartLog School Information Management System - Attendance tracking via QR scanning" | Out-Null
    sc.exe failure $Script:ServiceName reset=86400 actions=restart/5000/restart/10000/restart/30000 | Out-Null

    Write-Success "Windows Service '$($Script:ServiceName)' created"

    # Start the service
    Write-Detail "Starting service..."
    sc.exe start $Script:ServiceName | Out-Null
    Start-Sleep -Seconds 5

    $svc = Get-Service -Name $Script:ServiceName -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -eq 'Running') {
        Write-Success "Service is running"
    }
    else {
        Write-Warn "Service may still be starting. Check status with: sc.exe query $($Script:ServiceName)"
    }
}
else {
    Write-Detail "Skipped Windows Service installation"
    Write-Detail "You can run the app manually with:"
    Write-Detail "  dotnet run --project src/SmartLog.Web"
}

# ============================================================
# Summary
# ============================================================
Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "  ║                                                  ║" -ForegroundColor Green
Write-Host "  ║       Setup Complete!                            ║" -ForegroundColor Green
Write-Host "  ║                                                  ║" -ForegroundColor Green
Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "  ┌─────────────────────────────────────────────────┐" -ForegroundColor DarkGray
Write-Host "  │  ACCESS THE APPLICATION                         │" -ForegroundColor DarkGray
Write-Host "  ├─────────────────────────────────────────────────┤" -ForegroundColor DarkGray
Write-Host "  │  Local:   http://localhost:$($Script:HttpPort)                │" -ForegroundColor White
if ($currentIP) {
Write-Host "  │  Network: http://$($currentIP):$($Script:HttpPort)$(' ' * (22 - $currentIP.Length - "$($Script:HttpPort)".Length))│" -ForegroundColor White
}
if ($Script:EnableHttps) {
Write-Host "  ├─────────────────────────────────────────────────┤" -ForegroundColor DarkGray
Write-Host "  │  HTTPS (encrypted):                             │" -ForegroundColor DarkGray
Write-Host "  │  Local:   https://localhost:$($Script:HttpsPort)               │" -ForegroundColor White
if ($currentIP) {
Write-Host "  │  Network: https://$($currentIP):$($Script:HttpsPort)$(' ' * (21 - $currentIP.Length - "$($Script:HttpsPort)".Length))│" -ForegroundColor White
}
}
Write-Host "  └─────────────────────────────────────────────────┘" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  ┌─────────────────────────────────────────────────┐" -ForegroundColor DarkGray
Write-Host "  │  DEFAULT LOGIN ACCOUNTS                         │" -ForegroundColor DarkGray
Write-Host "  ├─────────────────────────────────────────────────┤" -ForegroundColor DarkGray
Write-Host "  │  SuperAdmin:  super.admin                       │" -ForegroundColor White
Write-Host "  │  Admin:       admin.amy                         │" -ForegroundColor White
Write-Host "  │  Password:    (the one you entered above)       │" -ForegroundColor White
Write-Host "  └─────────────────────────────────────────────────┘" -ForegroundColor DarkGray
Write-Host ""

# Show credentials that need to be saved
$dbPwPad    = [Math]::Max(0, 47 - $dbPassword.Length)
$adminPwPad = [Math]::Max(0, 47 - $adminPassword.Length)

Write-Host "  ┌─────────────────────────────────────────────────┐" -ForegroundColor Yellow
Write-Host "  │  SAVE THESE CREDENTIALS (shown once only)       │" -ForegroundColor Yellow
Write-Host "  ├─────────────────────────────────────────────────┤" -ForegroundColor Yellow
Write-Host "  │                                                 │" -ForegroundColor Yellow
Write-Host "  │  Database Password:                             │" -ForegroundColor Yellow
Write-Host "  │  $dbPassword$(' ' * $dbPwPad)│" -ForegroundColor White
Write-Host "  │                                                 │" -ForegroundColor Yellow
Write-Host "  │  HMAC Secret Key (for Scanner App):             │" -ForegroundColor Yellow
Write-Host "  │  $hmacSecret" -ForegroundColor White
Write-Host "  │                                                 │" -ForegroundColor Yellow
Write-Host "  │  Admin Password:                                │" -ForegroundColor Yellow
Write-Host "  │  $adminPassword$(' ' * $adminPwPad)│" -ForegroundColor White
if ($Script:EnableHttps) {
Write-Host "  │                                                 │" -ForegroundColor Yellow
Write-Host "  │  TLS Certificate Thumbprint (for Scanner App):  │" -ForegroundColor Yellow
Write-Host "  │  $($Script:CertThumbprint)" -ForegroundColor White
}
Write-Host "  │                                                 │" -ForegroundColor Yellow
Write-Host "  └─────────────────────────────────────────────────┘" -ForegroundColor Yellow
Write-Host ""
Write-Host "  IMPORTANT:" -ForegroundColor Red
Write-Host "  1. Save the HMAC Secret Key -- Scanner App needs it" -ForegroundColor Yellow
Write-Host "  2. Change default account passwords after first login" -ForegroundColor Yellow
Write-Host "  3. Set a static IP for this machine (see docs/DEPLOYMENT.md)" -ForegroundColor Yellow
if ($Script:EnableHttps) {
Write-Host "  4. Copy the TLS thumbprint into each Scanner App's HTTPS settings" -ForegroundColor Yellow
Write-Host "     Scanner URL: https://$($currentIP ?? $env:COMPUTERNAME):$($Script:HttpsPort)" -ForegroundColor Cyan
}
Write-Host ""

# Service management commands
Write-Host "  ┌─────────────────────────────────────────────────┐" -ForegroundColor DarkGray
Write-Host "  │  SERVICE MANAGEMENT                             │" -ForegroundColor DarkGray
Write-Host "  ├─────────────────────────────────────────────────┤" -ForegroundColor DarkGray
Write-Host "  │  Status:   sc.exe query SmartLogWeb             │" -ForegroundColor Gray
Write-Host "  │  Stop:     sc.exe stop SmartLogWeb              │" -ForegroundColor Gray
Write-Host "  │  Start:    sc.exe start SmartLogWeb             │" -ForegroundColor Gray
Write-Host "  │  Logs:     C:\SmartLog\logs\                    │" -ForegroundColor Gray
Write-Host "  │  Backups:  C:\SmartLogBackups\                  │" -ForegroundColor Gray
Write-Host "  └─────────────────────────────────────────────────┘" -ForegroundColor DarkGray
Write-Host ""

Read-Host "  Press Enter to exit"
