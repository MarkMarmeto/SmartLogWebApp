<#
.SYNOPSIS
    Generate a secure HMAC secret key for SmartLog QR code signing.

.DESCRIPTION
    Generates a cryptographically secure 32-byte (256-bit) key encoded
    as Base64. Use this key in both the Web App and Scanner App.

.NOTES
    Usage: powershell -ExecutionPolicy Bypass -File .\GenerateHMAC.ps1
#>

$keyBytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($keyBytes)
$rng.Dispose()
$hmacKey = [Convert]::ToBase64String($keyBytes)

Write-Host ""
Write-Host "  ======================================================" -ForegroundColor Cyan
Write-Host "       SmartLog — HMAC Secret Key Generator              " -ForegroundColor Cyan
Write-Host "  ======================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Your new HMAC Secret Key:" -ForegroundColor Gray
Write-Host ""
Write-Host "  $hmacKey" -ForegroundColor Green
Write-Host ""
Write-Host "  ======================================================" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  IMPORTANT:" -ForegroundColor Yellow
Write-Host "  1. Copy this key and save it securely" -ForegroundColor Gray
Write-Host "  2. Set it as environment variable on the server:" -ForegroundColor Gray
Write-Host "     setx /M SMARTLOG_HMAC_SECRET_KEY `"$hmacKey`"" -ForegroundColor White
Write-Host "  3. Or update it in Admin > Settings > QRCode tab" -ForegroundColor Gray
Write-Host "  4. Enter the SAME key in the Scanner App Setup Wizard" -ForegroundColor Gray
Write-Host ""
Write-Host "  WARNING: Changing the HMAC key will invalidate ALL" -ForegroundColor Red
Write-Host "  existing student QR codes. Students will need new ones." -ForegroundColor Red
Write-Host ""

# Copy to clipboard if available
try {
    $hmacKey | Set-Clipboard
    Write-Host "  Key has been copied to clipboard." -ForegroundColor Green
}
catch {
    # Clipboard not available
}

Write-Host ""
Read-Host "  Press Enter to exit"
