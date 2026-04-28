using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Device entity for registered scanner devices.
/// Implements US0028 (Register Scanner Device).
/// </summary>
public class Device
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Location { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Hashed API key for device authentication.
    /// Never store plain text API keys.
    /// </summary>
    [Required]
    public string ApiKeyHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who registered this device.
    /// </summary>
    public string RegisteredBy { get; set; } = string.Empty;

    public DateTime? LastSeenAt { get; set; }

    // US0119: Heartbeat snapshot fields (overwrite-only, no history table)
    [StringLength(50)]
    public string? AppVersion { get; set; }

    [StringLength(100)]
    public string? OsVersion { get; set; }

    public int? BatteryPercent { get; set; }

    public bool? IsCharging { get; set; }

    [StringLength(20)]
    public string? NetworkType { get; set; }

    public DateTime? LastHeartbeatAt { get; set; }

    public int? QueuedScansCount { get; set; }

    // Navigation properties
    public virtual ApplicationUser? RegisteredByUser { get; set; }
    public virtual ICollection<Scan> Scans { get; set; } = new List<Scan>();
}
