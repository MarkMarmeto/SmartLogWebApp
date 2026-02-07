using System.Security.Cryptography;
using System.Text;
using QRCoder;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

/// <summary>
/// Implementation of QR code generation service with HMAC-SHA256 signing.
/// Implements US0019 (Generate Student QR Code).
/// </summary>
public class QrCodeService : IQrCodeService
{
    private readonly string _secretKey;
    private readonly ILogger<QrCodeService> _logger;

    public QrCodeService(IConfiguration configuration, ILogger<QrCodeService> logger)
    {
        _secretKey = configuration["QrCode:HmacSecretKey"]
            ?? throw new InvalidOperationException("QR Code HMAC secret key not configured");
        _logger = logger;
    }

    /// <summary>
    /// US0019-AC1, AC2, AC3: Generate QR code with HMAC signature.
    /// Format: SMARTLOG:{studentId}:{timestamp}:{hmacSignature}
    /// </summary>
    public async Task<QrCode> GenerateQrCodeAsync(string studentId)
    {
        // US0019-AC3: Get Unix timestamp
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // US0019-AC3: Compute HMAC-SHA256
        var dataToSign = $"{studentId}:{timestamp}";
        var hmacSignature = ComputeHmac(dataToSign);

        // US0019-AC2: Build QR payload
        var payload = $"SMARTLOG:{studentId}:{timestamp}:{hmacSignature}";

        // Generate QR code image
        var qrImageBase64 = await Task.Run(() => GenerateQrImage(payload));

        // US0019-AC4: Create QR code entity
        var qrCode = new QrCode
        {
            Payload = payload,
            HmacSignature = hmacSignature,
            IssuedAt = DateTime.UtcNow,
            IsValid = true,
            QrImageBase64 = qrImageBase64
        };

        _logger.LogInformation("Generated QR code for student {StudentId}", studentId);

        return qrCode;
    }

    /// <summary>
    /// Verify QR code HMAC signature using constant-time comparison.
    /// US0031-AC7: Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    public bool VerifyQrCode(string payload, string hmacSignature)
    {
        var parts = payload.Split(':');
        if (parts.Length != 4 || parts[0] != "SMARTLOG")
        {
            return false;
        }

        var studentId = parts[1];
        var timestamp = parts[2];
        var dataToVerify = $"{studentId}:{timestamp}";
        var expectedHmac = ComputeHmac(dataToVerify);

        // US0031-AC7: Constant-time comparison to prevent timing attacks
        try
        {
            var expectedBytes = Convert.FromBase64String(expectedHmac);
            var providedBytes = Convert.FromBase64String(hmacSignature);
            return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parse QR payload and extract components.
    /// </summary>
    public (string StudentId, long Timestamp, string Signature)? ParseQrPayload(string payload)
    {
        var parts = payload.Split(':');
        if (parts.Length != 4 || parts[0] != "SMARTLOG")
        {
            return null;
        }

        if (!long.TryParse(parts[2], out var timestamp))
        {
            return null;
        }

        return (parts[1], timestamp, parts[3]);
    }

    private string ComputeHmac(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }

    private string GenerateQrImage(string payload)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);

        var qrCodeBytes = qrCode.GetGraphic(20); // 20 pixels per module
        return Convert.ToBase64String(qrCodeBytes);
    }
}
