using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Reusable anonymous visitor QR pass.
/// Implements US0072 (Visitor Pass Entity & QR Generation).
/// </summary>
public class VisitorPass
{
    public Guid Id { get; set; }

    /// <summary>
    /// Sequential pass number (1, 2, 3, ...).
    /// </summary>
    public int PassNumber { get; set; }

    /// <summary>
    /// Human-readable code, e.g., "VISITOR-001".
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Full QR payload: SMARTLOG-V:{code}:{timestamp}:{hmac}
    /// </summary>
    [Required]
    [StringLength(500)]
    public string QrPayload { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 signature (Base64 encoded).
    /// </summary>
    [Required]
    [StringLength(100)]
    public string HmacSignature { get; set; } = string.Empty;

    /// <summary>
    /// Base64-encoded QR code image (PNG).
    /// </summary>
    public string? QrImageBase64 { get; set; }

    /// <summary>
    /// Whether this pass can be used for scanning.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this pass was generated.
    /// </summary>
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current pass status: Available, InUse, or Deactivated.
    /// </summary>
    [Required]
    [StringLength(20)]
    public string CurrentStatus { get; set; } = "Available";

    // Navigation properties
    public virtual ICollection<VisitorScan> VisitorScans { get; set; } = new List<VisitorScan>();
}
