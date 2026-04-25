using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

/// <summary>
/// Service for generating and managing student QR codes with HMAC-SHA256 signing.
/// </summary>
public interface IQrCodeService
{
    /// <summary>
    /// Generate a new QR code for a student with HMAC signature.
    /// </summary>
    Task<QrCode> GenerateQrCodeAsync(string studentId);

    /// <summary>
    /// Verify a QR code payload against its HMAC signature.
    /// </summary>
    Task<bool> VerifyQrCodeAsync(string payload, string hmacSignature);

    /// <summary>
    /// Parse QR code payload and extract components.
    /// </summary>
    (string StudentId, long Timestamp, string Signature)? ParseQrPayload(string payload);

    /// <summary>
    /// Parse a visitor QR payload (SMARTLOG-V: prefix) and extract components.
    /// Returns null if the payload is not a valid visitor QR.
    /// </summary>
    (string Code, long Timestamp, string Signature)? ParseVisitorQrPayload(string payload);

    /// <summary>
    /// Verify a visitor QR code's HMAC signature using constant-time comparison.
    /// HMAC is computed over "{code}:{timestamp}".
    /// </summary>
    Task<bool> VerifyVisitorQrAsync(string code, long timestamp, string signature);
}
