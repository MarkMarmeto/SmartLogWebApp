using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Visitor scan record (entry/exit tracking for visitor passes).
/// Implements US0072 (Visitor Pass Entity).
/// </summary>
public class VisitorScan
{
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to VisitorPass.
    /// </summary>
    public Guid VisitorPassId { get; set; }

    /// <summary>
    /// Foreign key to the scanner device that captured this scan.
    /// </summary>
    public Guid DeviceId { get; set; }

    /// <summary>
    /// ENTRY or EXIT scan type.
    /// </summary>
    [Required]
    [StringLength(20)]
    public string ScanType { get; set; } = string.Empty;

    /// <summary>
    /// When the scan occurred according to the device clock.
    /// </summary>
    public DateTime ScannedAt { get; set; }

    /// <summary>
    /// When the scan was received by the server.
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Scan status: ACCEPTED, DUPLICATE, REJECTED_PASS_INACTIVE.
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Optional reference to the academic year for reporting.
    /// </summary>
    public Guid? AcademicYearId { get; set; }

    // Navigation properties
    public virtual VisitorPass VisitorPass { get; set; } = null!;
    public virtual Device Device { get; set; } = null!;
    public virtual AcademicYear? AcademicYear { get; set; }
}
