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
    bool VerifyQrCode(string payload, string hmacSignature);

    /// <summary>
    /// Parse QR code payload and extract components.
    /// </summary>
    (string StudentId, long Timestamp, string Signature)? ParseQrPayload(string payload);
}
