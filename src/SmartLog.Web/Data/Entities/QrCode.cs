using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// QR Code entity for student identity verification.
/// Format: SMARTLOG:{studentId}:{timestamp}:{hmacSignature}
/// </summary>
public class QrCode
{
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to Student
    /// </summary>
    public Guid StudentId { get; set; }

    /// <summary>
    /// Full QR code payload
    /// Format: SMARTLOG:{studentId}:{timestamp}:{hmacSignature}
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 signature (Base64 encoded)
    /// </summary>
    [Required]
    [StringLength(100)]
    public string HmacSignature { get; set; } = string.Empty;

    /// <summary>
    /// When the QR code was issued
    /// </summary>
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this QR code is still valid (false if regenerated)
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Base64 encoded QR code image (PNG)
    /// </summary>
    public string? QrImageBase64 { get; set; }

    // Navigation property
    public virtual Student Student { get; set; } = null!;
}
