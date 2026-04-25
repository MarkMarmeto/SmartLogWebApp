using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Tracks a batch SMS broadcast (announcement or emergency)
/// </summary>
public class Broadcast
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ANNOUNCEMENT or EMERGENCY
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Raw message body (before template wrapping)
    /// </summary>
    [Required]
    [StringLength(160)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Language sent: EN or FIL
    /// </summary>
    [StringLength(10)]
    public string? Language { get; set; }

    /// <summary>
    /// JSON array of grade codes, null = all grades
    /// </summary>
    [StringLength(200)]
    public string? AffectedGrades { get; set; }

    /// <summary>
    /// JSON array of program codes, null = all programs
    /// </summary>
    [StringLength(500)]
    public string? AffectedPrograms { get; set; }

    /// <summary>
    /// Scheduled delivery time in UTC; null = immediate
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// When the last SMS in this broadcast was sent
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// UserId of the staff member who created the broadcast
    /// </summary>
    [StringLength(450)]
    public string? CreatedByUserId { get; set; }

    /// <summary>
    /// Display name of the staff member (snapshot at creation)
    /// </summary>
    [StringLength(200)]
    public string? CreatedByName { get; set; }

    /// <summary>
    /// US0055: Override gateway for this broadcast. "GSM_MODEM" or "SEMAPHORE". Null = use system default.
    /// </summary>
    [StringLength(20)]
    public string? PreferredProvider { get; set; }

    public BroadcastStatus Status { get; set; } = BroadcastStatus.Pending;

    /// <summary>
    /// Total recipients queued (both primary + alternate phones)
    /// </summary>
    public int TotalRecipients { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ICollection<SmsQueue> Messages { get; set; } = new List<SmsQueue>();
}

public enum BroadcastStatus
{
    Pending = 0,
    Scheduled = 1,
    Sending = 2,
    Sent = 3,
    Cancelled = 4
}
