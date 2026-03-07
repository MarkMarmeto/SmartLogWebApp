using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// SMS queue entry for async message processing
/// </summary>
public class SmsQueue
{
    public long Id { get; set; }

    [Required]
    [StringLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// SMS content - max 320 chars for multi-part support
    /// </summary>
    [Required]
    [StringLength(320)]
    public string Message { get; set; } = string.Empty;

    [Required]
    public SmsStatus Status { get; set; } = SmsStatus.Pending;

    [Required]
    public SmsPriority Priority { get; set; } = SmsPriority.Normal;

    /// <summary>
    /// Message type: ATTENDANCE, CALENDAR, EMERGENCY, CUSTOM
    /// </summary>
    [Required]
    [StringLength(50)]
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Optional reference to student if attendance/parent notification
    /// </summary>
    public int? StudentId { get; set; }

    public int RetryCount { get; set; } = 0;

    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Next retry time with exponential backoff
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// Provider used: GSM_MODEM or SEMAPHORE
    /// </summary>
    [StringLength(50)]
    public string? Provider { get; set; }

    /// <summary>
    /// External message ID from provider
    /// </summary>
    [StringLength(100)]
    public string? ProviderMessageId { get; set; }

    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Scheduled delivery time — null = send immediately, future date = hold until then
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    // Navigation properties
    public virtual Student? Student { get; set; }
}

public enum SmsStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3,
    Cancelled = 4
}

public enum SmsPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Emergency = 3
}
