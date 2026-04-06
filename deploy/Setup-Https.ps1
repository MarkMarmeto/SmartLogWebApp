#Requires -RunAsAdministrator
<#
.SYNOPSIS
    SmartLog — HTTPS Setup Script (Self-Signed Certificate)

.DESCRIPTION
    Generates a self-signed SSL certificate for on-premise LAN deployment,
    exports it to C:\SmartLog\smartlog.pfx, and configures the required
    environment variables so SmartLog serves HTTPS on port 5051.

    After running this script, restart the SmartLog service:
      sc stop SmartLogWeb && sc start SmartLogWeb

    Scanner devices and browsers should then connect to:
      https://<server-ip>:5051

    NOTE: Self-signed certificates will show a browser warning on first visit.
    Click "Advanced → Proceed" to continue. The cert is trusted on this machine.

.PARAMETER Hostname
    The server hostname or IP address for the certificate (default: machine hostname).

.PARAMETER CertPassword
    Password for the PFX file. Auto-generated if not provided.

.PARAMETER ValidDays
    Certificate validity in days (default: 1095 = 3 years).
#>

[CmdletBinding()]
param(
    [string]$Hostname    = $env:COMPUTERNAME,
    [string]$CertPassword = "",
    [int]$ValidDays       = 1095
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$CertPath    = "C:\SmartLog\smartlog.pfx"
$ServiceName = "SmartLogWeb"

function Write-Step  { param([string]$m) Write-Host "`n  $m" -ForegroundColor Cyan }
function Write-Ok    { param([string]$m) Write-Host "  [OK] $m" -ForegroundColor Green }
function Write-Warn  { param([string]$m) Write-Host "  [!!] $m" -ForegroundColor Yellow }
function Write-Fail  { param([string]$m) Write-Host "  [XX] $m" -ForegroundColor Red }

Write-Host ""
Write-Host "  SmartLog HTTPS Setup" -ForegroundColor Cyan
Write-Host "  $('=' * 50)" -ForegroundColor DarkGray
Write-Host "  Hostname : $Hostname"
Write-Host "  Cert path: $CertPath"
Write-Host "  Valid for: $ValidDays days"

# ── Auto-generate password if not provided ────────────────────
if ([string]::IsNullOrEmpty($CertPassword)) {
    $chars = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$'
    $CertPassword = -join ((1..24) | ForEach-Object { $chars[(Get-Random -Maximum $chars.Length)] })
    Write-Warn "No password provided — auto-generated a secure password."
}

# ── Step 1: Generate self-signed certificate ──────────────────
Write-Step "[1/4] Generating self-signed certificate..."

# Build SAN list: hostname + localhost + local IP addresses
$sanList = @($Hostname, "localhost", "127.0.0.1")
$localIps = (Get-NetIPAddress -AddressFamily IPv4 |
             Where-Object { $_.InterfaceAlias -notmatch "Loopback" } |
             Select-Object -ExpandProperty IPAddress)
$sanList += $localIps

Write-Host "  SANs: $($sanList -join ', ')"

$cert = New-SelfSignedCertificate `
    -DnsName $sanList `
    -CertStoreLocation "cert:\LocalMachine\My" `
    -NotAfter (Get-Date).AddDays($ValidDays) `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -HashAlgorithm SHA256 `
    -KeyUsage DigitalSignature, KeyEncipherment `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1") # ServerAuthentication EKU

Write-Ok "Certificate generated — thumbprint: $($cert.Thumbprint)"

# ── Step 2: Trust the certificate on this machine ─────────────
Write-Step "[2/4] Trusting certificate on this machine..."

$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store(
    [System.Security.Cryptography.X509Certificates.StoreName]::Root,
    [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
$rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
$rootStore.Add($cert)
$rootStore.Close()

Write-Ok "Certificate added to Trusted Root CA store"

# ── Step 3: Export to PFX ─────────────────────────────────────
Write-Step "[3/4] Exporting certificate to PFX..."

New-Item -ItemType Directory -Force -Path (Split-Path $CertPath) | Out-Null

$securePassword = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $CertPath -Password $securePassword | Out-Null

Write-Ok "PFX exported → $CertPath"

# ── Step 4: Set environment variables ────────────────────────
Write-Step "[4/4] Setting environment variables..."

[System.Environment]::SetEnvironmentVariable("SMARTLOG_CERT_PATH",     $CertPath,     "Machine")
[System.Environment]::SetEnvironmentVariable("SMARTLOG_CERT_PASSWORD", $CertPassword, "Machine")

Write-Ok "SMARTLOG_CERT_PATH     = $CertPath"
Write-Ok "SMARTLOG_CERT_PASSWORD = (set, not shown)"

# ── Restart service if running ────────────────────────────────
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq "Running") {
    Write-Step "Restarting $ServiceName service..."
    Restart-Service -Name $ServiceName -Force
    Write-Ok "Service restarted"
}

# ── Summary ───────────────────────────────────────────────────
Write-Host ""
Write-Host "  $('=' * 50)" -ForegroundColor DarkGray
Write-Host "  HTTPS setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "  SmartLog is now accessible at:"
Write-Host "    https://$Hostname`:5051  (HTTPS)" -ForegroundColor Green
Write-Host "    http://$Hostname`:5050   (HTTP — redirects to HTTPS)"
Write-Host ""
Write-Host "  On first browser visit you may see a security warning." -ForegroundColor Yellow
Write-Host "  Click Advanced → Proceed to accept the self-signed cert." -ForegroundColor Yellow
Write-Host ""
Write-Host "  To update scanner devices, set their server URL to:"
Write-Host "    https://$Hostname`:5051" -ForegroundColor Cyan
Write-Host ""
