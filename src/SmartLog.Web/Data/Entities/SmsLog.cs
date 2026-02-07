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
    public int? StudentId { get; set; }

    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    // Navigation properties
    public virtual SmsQueue? Queue { get; set; }
    public virtual Student? Student { get; set; }
}
