using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Audit log for all SMS delivery attempts
/// </summary>
public class SmsLog
{
    public long Id { get; set; }

    /// <summary>
    /// Optional reference to queue entry
    /// </summary>
    public long? QueueId { get; set; }

    [Required]
    [StringLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(320)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Final status: SENT, FAILED, CANCELLED
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// US0057: Message type copied from SmsQueue at send time (ATTENDANCE, NO_SCAN_ALERT, PERSONAL, etc.)
    /// </summary>
    [StringLength(50)]
    public string? MessageType { get; set; }

    /// <summary>
    /// Provider that handled the message
    /// </summary>
    [StringLength(50)]
    public string? Provider { get; set; }

    /// <summary>
    /// Number of message parts for long SMS
    /// </summary>
    public int MessageParts { get; set; } = 1;

    /// <summary>
    /// Time taken to send in milliseconds
    /// </summary>
    public int ProcessingTimeMs { get; set; }

    /// <summary>
    /// Optional reference to student
    /// </summary>
    public Guid? StudentId { get; set; }

    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// External message ID from provider for matching delivery report webhooks
    /// </summary>
    [StringLength(100)]
    public string? ProviderMessageId { get; set; }

    /// <summary>
    /// Delivery status from provider: DELIVERED / UNDELIVERED / REJECTED
    /// </summary>
    [StringLength(30)]
    public string? DeliveryStatus { get; set; }

    /// <summary>
    /// When provider confirmed delivery
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    // Navigation properties
    public virtual SmsQueue? Queue { get; set; }
    public virtual Student? Student { get; set; }
}
