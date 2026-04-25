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
    private readonly IAppSettingsService _appSettingsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<QrCodeService> _logger;

    public QrCodeService(
        IAppSettingsService appSettingsService,
        IConfiguration configuration,
        ILogger<QrCodeService> logger)
    {
        _appSettingsService = appSettingsService;
        _configuration = configuration;
        _logger = logger;
    }

    private async Task<string> GetSecretKeyAsync()
    {
        // Priority: app settings DB (admin-managed) > environment variable > appsettings.json
        // DB takes highest priority so admin changes via Settings UI always take effect.
        // Env var serves as initial/fallback configuration before first DB update.
        var key = await _appSettingsService.GetAsync("QRCode.HmacSecretKey");
        if (!string.IsNullOrEmpty(key) && !key.StartsWith("${"))
            return key;

        var envKey = Environment.GetEnvironmentVariable("SMARTLOG_HMAC_SECRET_KEY");
        if (!string.IsNullOrEmpty(envKey))
            return envKey;

        var configKey = _configuration["QrCode:HmacSecretKey"];
        if (!string.IsNullOrEmpty(configKey) && !configKey.StartsWith("${"))
            return configKey;

        throw new InvalidOperationException("QR Code HMAC secret key not configured. Set SMARTLOG_HMAC_SECRET_KEY environment variable or update it in Admin Settings.");
    }

    /// <summary>
    /// US0019-AC1, AC2, AC3: Generate QR code with HMAC signature.
    /// Format: SMARTLOG:{studentId}:{timestamp}:{hmacSignature}
    /// </summary>
    public async Task<QrCode> GenerateQrCodeAsync(string studentId)
    {
        var secretKey = await GetSecretKeyAsync();

        // US0019-AC3: Get Unix timestamp
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // US0019-AC3: Compute HMAC-SHA256
        var dataToSign = $"{studentId}:{timestamp}";
        var hmacSignature = ComputeHmac(dataToSign, secretKey);

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
    public async Task<bool> VerifyQrCodeAsync(string payload, string hmacSignature)
    {
        var parts = payload.Split(':');
        if (parts.Length != 4 || parts[0] != "SMARTLOG")
        {
            return false;
        }

        var secretKey = await GetSecretKeyAsync();

        var studentId = parts[1];
        var timestamp = parts[2];
        var dataToVerify = $"{studentId}:{timestamp}";
        var expectedHmac = ComputeHmac(dataToVerify, secretKey);

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

    /// <summary>
    /// Parse visitor QR payload: SMARTLOG-V:{code}:{timestamp}:{hmac}
    /// </summary>
    public (string Code, long Timestamp, string Signature)? ParseVisitorQrPayload(string payload)
    {
        var parts = payload.Split(':');
        if (parts.Length != 4 || parts[0] != "SMARTLOG-V")
        {
            return null;
        }

        if (!long.TryParse(parts[2], out var timestamp))
        {
            return null;
        }

        // Validate code format: alphanumeric + hyphen only
        var code = parts[1];
        if (string.IsNullOrEmpty(code) || !code.All(c => char.IsLetterOrDigit(c) || c == '-'))
        {
            return null;
        }

        return (code, timestamp, parts[3]);
    }

    /// <summary>
    /// Verify visitor QR HMAC: HMAC-SHA256("{code}:{timestamp}") with constant-time comparison.
    /// </summary>
    public async Task<bool> VerifyVisitorQrAsync(string code, long timestamp, string signature)
    {
        var secretKey = await GetSecretKeyAsync();

        var dataToVerify = $"{code}:{timestamp}";
        var expectedHmac = ComputeHmac(dataToVerify, secretKey);

        try
        {
            var expectedBytes = Convert.FromBase64String(expectedHmac);
            var providedBytes = Convert.FromBase64String(signature);
            return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeHmac(string data, string secretKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
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
