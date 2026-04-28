using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Audit log entry for tracking security-relevant events.
/// Implements US0002-AC6 and future audit requirements.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// The user ID affected by the action (e.g., locked user).
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// The user ID who performed the action (e.g., Super Admin who unlocked).
    /// Null for system-triggered actions.
    /// </summary>
    public string? PerformedByUserId { get; set; }

    [StringLength(500)]
    public string? Details { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? UserAgent { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When true, this row is exempt from retention purge (RA 10173 legal hold).
    /// </summary>
    public bool LegalHold { get; set; } = false;

    // Username snapshots captured at write time — no FK to AspNetUsers.
    // Rows remain readable even if the referenced user is later renamed or deleted.
    [StringLength(256)]
    public string? UserName { get; set; }

    [StringLength(256)]
    public string? PerformedByUserName { get; set; }
}
