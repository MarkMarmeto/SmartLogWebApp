using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Scan entity for QR code scan records.
/// Implements US0030 (Scan Ingestion API).
/// </summary>
public class Scan
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public Guid StudentId { get; set; }

    [Required]
    [StringLength(500)]
    public string QrPayload { get; set; } = string.Empty;

    /// <summary>
    /// When the scan occurred according to the device clock.
    /// </summary>
    public DateTime ScannedAt { get; set; }

    /// <summary>
    /// When the scan was received by the server.
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ENTRY or EXIT scan type.
    /// </summary>
    [Required]
    [StringLength(20)]
    public string ScanType { get; set; } = string.Empty;

    /// <summary>
    /// Scan status: ACCEPTED, DUPLICATE, REJECTED_INVALID_QR, REJECTED_STUDENT_INACTIVE, REJECTED_QR_INVALIDATED
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Optional reference to the academic year this scan belongs to.
    /// Used for historical reporting and filtering.
    /// </summary>
    public Guid? AcademicYearId { get; set; }

    // Navigation properties
    public virtual Device Device { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
    public virtual AcademicYear? AcademicYear { get; set; }
}
