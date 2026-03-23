#Requires -RunAsAdministrator
<#
.SYNOPSIS
    SmartLog - Network Configuration Helper

.DESCRIPTION
    Detects current network settings and helps configure a static IP
    address for LAN access. Shows available IP ranges based on your
    current network configuration.

.NOTES
    Run as Administrator: .\Setup-Network.ps1
    Or use: Setup-Network.bat
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ============================================================
# Helper Functions
# ============================================================
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
Write-Host "  ======================================================" -ForegroundColor Cyan
Write-Host "       SmartLog -- Network Configuration Helper          " -ForegroundColor Cyan
Write-Host "  ======================================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================
# Step 1: Detect Network Adapters
# ============================================================
Write-Host "  [1/3] Detecting Network Adapters" -ForegroundColor Cyan
Write-Host "  ==================================================" -ForegroundColor DarkGray

$adapters = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' }

if (-not $adapters) {
    Write-Warn "No active network adapters found!"
    Read-Host "  Press Enter to exit"
    exit 1
}

Write-Host ""
$adapterList = @()
$index = 1
foreach ($adapter in $adapters) {
    $ipConfig = Get-NetIPAddress -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue
    $gateway = Get-NetRoute -InterfaceIndex $adapter.ifIndex -DestinationPrefix "0.0.0.0/0" -ErrorAction SilentlyContinue | Select-Object -First 1
    $dns = Get-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue

    $adapterInfo = @{
        Index     = $index
        Name      = $adapter.Name
        Desc      = $adapter.InterfaceDescription
        IfIndex   = $adapter.ifIndex
        IP        = if ($ipConfig) { $ipConfig.IPAddress } else { "N/A" }
        Prefix    = if ($ipConfig) { $ipConfig.PrefixLength } else { 0 }
        Gateway   = if ($gateway) { $gateway.NextHop } else { "N/A" }
        DNS       = if ($dns -and $dns.ServerAddresses) { ($dns.ServerAddresses -join ", ") } else { "N/A" }
        DHCP      = if ($ipConfig -and $ipConfig.PrefixOrigin -eq 'Dhcp') { "Yes" } else { "No" }
    }
    $adapterList += $adapterInfo

    Write-Host "  [$index] $($adapter.Name) ($($adapter.InterfaceDescription))" -ForegroundColor White
    Write-Host "      Current IP:    $($adapterInfo.IP)/$($adapterInfo.Prefix)" -ForegroundColor Gray
    Write-Host "      Gateway:       $($adapterInfo.Gateway)" -ForegroundColor Gray
    Write-Host "      DNS:           $($adapterInfo.DNS)" -ForegroundColor Gray
    Write-Host "      DHCP:          $($adapterInfo.DHCP)" -ForegroundColor Gray
    Write-Host ""
    $index++
}

# ============================================================
# Step 2: Show Available IP Ranges
# ============================================================
Write-Host "  [2/3] IP Address Ranges" -ForegroundColor Cyan
Write-Host "  ==================================================" -ForegroundColor DarkGray
Write-Host ""

# Select adapter
$selectedIndex = 1
if ($adapterList.Count -gt 1) {
    $selectedInput = Read-Host "  Select adapter number [1]"
    if (-not [string]::IsNullOrWhiteSpace($selectedInput)) {
        $selectedIndex = [int]$selectedInput
    }
}

$selected = $adapterList[$selectedIndex - 1]
Write-Host ""
Write-Host "  Selected: $($selected.Name)" -ForegroundColor White
Write-Host ""

# Calculate network range
$currentIP = $selected.IP
$prefix = $selected.Prefix
$gateway = $selected.Gateway

if ($currentIP -ne "N/A" -and $currentIP -match '(\d+)\.(\d+)\.(\d+)\.(\d+)') {
    $octet1 = [int]$Matches[1]
    $octet2 = [int]$Matches[2]
    $octet3 = [int]$Matches[3]
    $octet4 = [int]$Matches[4]

    # Determine network class and usable range
    $networkBase = "$octet1.$octet2.$octet3"

    # Calculate subnet mask from prefix
    $subnetBits = $prefix
    if ($subnetBits -eq 24) {
        $subnetMask = "255.255.255.0"
        $rangeStart = "$networkBase.2"
        $rangeEnd = "$networkBase.254"
        $totalHosts = 253
    }
    elseif ($subnetBits -eq 16) {
        $subnetMask = "255.255.0.0"
        $rangeStart = "$octet1.$octet2.0.2"
        $rangeEnd = "$octet1.$octet2.255.254"
        $totalHosts = 65533
    }
    else {
        $subnetMask = "255.255.255.0"
        $rangeStart = "$networkBase.2"
        $rangeEnd = "$networkBase.254"
        $totalHosts = 253
    }

    Write-Host "  Network Information:" -ForegroundColor Yellow
    Write-Host "  --------------------------------------------------" -ForegroundColor DarkGray
    Write-Host "  Current IP:        $currentIP" -ForegroundColor White
    Write-Host "  Subnet Mask:       $subnetMask (/$prefix)" -ForegroundColor White
    Write-Host "  Gateway:           $gateway" -ForegroundColor White
    Write-Host "  Network:           $networkBase.0/$prefix" -ForegroundColor White
    Write-Host ""
    Write-Host "  Available IP Range:" -ForegroundColor Yellow
    Write-Host "  --------------------------------------------------" -ForegroundColor DarkGray
    Write-Host "  From:              $rangeStart" -ForegroundColor Green
    Write-Host "  To:                $rangeEnd" -ForegroundColor Green
    Write-Host "  Total addresses:   $totalHosts" -ForegroundColor White
    Write-Host ""

    # Suggest a static IP (use high range to avoid DHCP conflicts)
    # DHCP usually assigns from .100-.200, so suggest .10-.50 range
    $suggestedIP = "$networkBase.10"

    Write-Host "  Recommended Static IP Range:" -ForegroundColor Yellow
    Write-Host "  --------------------------------------------------" -ForegroundColor DarkGray
    Write-Host "  Use IPs in the range: $networkBase.10 to $networkBase.50" -ForegroundColor Green
    Write-Host "  (Low range avoids conflicts with DHCP which typically" -ForegroundColor Gray
    Write-Host "   assigns from $networkBase.100 and above)" -ForegroundColor Gray
    Write-Host ""

    # Check which IPs are already in use in the suggested range
    Write-Host "  Checking which IPs are available..." -ForegroundColor Gray
    $availableIPs = @()
    for ($i = 10; $i -le 20; $i++) {
        $testIP = "$networkBase.$i"
        $ping = Test-Connection -ComputerName $testIP -Count 1 -Quiet -ErrorAction SilentlyContinue
        if (-not $ping) {
            $availableIPs += $testIP
            if ($availableIPs.Count -ge 5) { break }
        }
    }

    if ($availableIPs.Count -gt 0) {
        Write-Host ""
        Write-Host "  Available IPs (not currently in use):" -ForegroundColor Green
        foreach ($ip in $availableIPs) {
            Write-Host "    - $ip" -ForegroundColor White
        }
        $suggestedIP = $availableIPs[0]
    }
    Write-Host ""

    # ============================================================
    # Step 3: Configure Static IP
    # ============================================================
    Write-Host "  [3/3] Configure Static IP" -ForegroundColor Cyan
    Write-Host "  ==================================================" -ForegroundColor DarkGray
    Write-Host ""

    if (Read-YesNo "Set a static IP address on this adapter?" $true) {
        $newIP = Read-Host "  Enter static IP address [$suggestedIP]"
        if ([string]::IsNullOrWhiteSpace($newIP)) { $newIP = $suggestedIP }

        # Validate IP format
        if ($newIP -notmatch '^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$') {
            Write-Warn "Invalid IP address format"
            Read-Host "  Press Enter to exit"
            exit 1
        }

        $newGateway = Read-Host "  Enter gateway [$gateway]"
        if ([string]::IsNullOrWhiteSpace($newGateway)) { $newGateway = $gateway }

        $newDNS = Read-Host "  Enter DNS server [$gateway]"
        if ([string]::IsNullOrWhiteSpace($newDNS)) { $newDNS = $newGateway }

        Write-Host ""
        Write-Host "  Summary:" -ForegroundColor Yellow
        Write-Host "    Adapter:     $($selected.Name)" -ForegroundColor White
        Write-Host "    IP Address:  $newIP" -ForegroundColor White
        Write-Host "    Subnet Mask: $subnetMask (/$prefix)" -ForegroundColor White
        Write-Host "    Gateway:     $newGateway" -ForegroundColor White
        Write-Host "    DNS:         $newDNS" -ForegroundColor White
        Write-Host ""

        if (Read-YesNo "Apply these settings? (your connection may drop briefly)" $false) {
            try {
                # Remove existing IP configuration
                $existingIP = Get-NetIPAddress -InterfaceIndex $selected.IfIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue
                if ($existingIP) {
                    Remove-NetIPAddress -InterfaceIndex $selected.IfIndex -AddressFamily IPv4 -Confirm:$false -ErrorAction SilentlyContinue
                }

                # Remove existing gateway
                Remove-NetRoute -InterfaceIndex $selected.IfIndex -DestinationPrefix "0.0.0.0/0" -Confirm:$false -ErrorAction SilentlyContinue

                # Set new static IP
                New-NetIPAddress -InterfaceIndex $selected.IfIndex -IPAddress $newIP -PrefixLength $prefix -DefaultGateway $newGateway | Out-Null
                Write-Success "Static IP set: $newIP"

                # Set DNS
                Set-DnsClientServerAddress -InterfaceIndex $selected.IfIndex -ServerAddresses $newDNS
                Write-Success "DNS set: $newDNS"

                Write-Host ""
                Write-Host "  SmartLog will be accessible at:" -ForegroundColor Yellow

                # Check SmartLog port
                $smartlogPort = [System.Environment]::GetEnvironmentVariable("ASPNETCORE_URLS", "Machine")
                if ($smartlogPort -match ':(\d+)') {
                    $port = $Matches[1]
                }
                else {
                    $port = "8080"
                }
                Write-Host "  http://$($newIP):$port" -ForegroundColor Green
                Write-Host ""
                Write-Host "  Use this URL in the Scanner App setup." -ForegroundColor Gray
            }
            catch {
                Write-Warn "Failed to set static IP: $_"
                Write-Host "  You can set it manually:" -ForegroundColor Yellow
                Write-Host "  Settings > Network > Ethernet > IP assignment > Edit > Manual" -ForegroundColor Gray
            }
        }
        else {
            Write-Host ""
            Write-Host "  To set it manually:" -ForegroundColor Yellow
            Write-Host "  1. Open Settings > Network & Internet > Ethernet" -ForegroundColor Gray
            Write-Host "  2. Click on your network adapter" -ForegroundColor Gray
            Write-Host "  3. Click 'Edit' next to IP assignment" -ForegroundColor Gray
            Write-Host "  4. Switch to 'Manual' and enable IPv4" -ForegroundColor Gray
            Write-Host "  5. Enter:" -ForegroundColor Gray
            Write-Host "     IP address:   $newIP" -ForegroundColor White
            Write-Host "     Subnet mask:  $subnetMask" -ForegroundColor White
            Write-Host "     Gateway:      $newGateway" -ForegroundColor White
            Write-Host "     Preferred DNS: $newDNS" -ForegroundColor White
        }
    }
    else {
        Write-Host ""
        Write-Host "  To set a static IP manually:" -ForegroundColor Yellow
        Write-Host "  1. Open Settings > Network & Internet > Ethernet" -ForegroundColor Gray
        Write-Host "  2. Click on your network adapter" -ForegroundColor Gray
        Write-Host "  3. Click 'Edit' next to IP assignment" -ForegroundColor Gray
        Write-Host "  4. Switch to 'Manual' and enable IPv4" -ForegroundColor Gray
        Write-Host "  5. Use an IP in the range: $rangeStart - $rangeEnd" -ForegroundColor Gray
        Write-Host "     Subnet mask:  $subnetMask" -ForegroundColor Gray
        Write-Host "     Gateway:      $gateway" -ForegroundColor Gray
    }
}
else {
    Write-Warn "Could not determine network configuration from current IP"
    Write-Host "  Run 'ipconfig' to view your network settings manually." -ForegroundColor Gray
}

Write-Host ""
Read-Host "  Press Enter to exit"
