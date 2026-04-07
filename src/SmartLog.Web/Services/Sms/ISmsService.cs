using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services.Sms;

/// <summary>
/// Core SMS service for queuing and managing notifications
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Queue attendance notification (entry/exit)
    /// </summary>
    Task QueueAttendanceNotificationAsync(Guid studentId, string scanType, DateTime scanTime, Guid scanId);

    /// <summary>
    /// Queue calendar event notifications (holiday/suspension)
    /// </summary>
    Task QueueCalendarEventNotificationsAsync(Guid calendarEventId);

    /// <summary>
    /// Queue emergency announcement to all or filtered by grade. Returns the Broadcast Id.
    /// </summary>
    Task<Guid> QueueEmergencyAnnouncementAsync(
        string message,
        string? language = null,
        List<string>? affectedGrades = null,
        string? createdByUserId = null,
        string? createdByName = null);

    /// <summary>
    /// Queue general announcement to all or filtered by grade. Returns the Broadcast Id.
    /// </summary>
    Task<Guid> QueueAnnouncementAsync(
        string message,
        string? language = null,
        List<string>? affectedGrades = null,
        DateTime? scheduledAt = null,
        string? createdByUserId = null,
        string? createdByName = null);

    /// <summary>
    /// Queue custom SMS message
    /// </summary>
    Task<long> QueueCustomSmsAsync(string phoneNumber, string message, SmsPriority priority = SmsPriority.Normal, string messageType = "CUSTOM", DateTime? scheduledAt = null);

    /// <summary>
    /// Cancel a queued SMS
    /// </summary>
    Task<bool> CancelSmsAsync(long queueId);

    /// <summary>
    /// Cancel all pending/scheduled messages belonging to a broadcast.
    /// Returns number of messages cancelled.
    /// </summary>
    Task<int> CancelBroadcastAsync(Guid broadcastId);

    /// <summary>
    /// Get paginated list of broadcasts ordered by creation date desc
    /// </summary>
    Task<List<Broadcast>> GetBroadcastsAsync(int page = 1, int pageSize = 20);

    /// <summary>
    /// Get a single broadcast with summary stats
    /// </summary>
    Task<Broadcast?> GetBroadcastAsync(Guid broadcastId);

    /// <summary>
    /// Get SMS statistics
    /// </summary>
    Task<SmsStatistics> GetStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Get count of scheduled (future) pending messages
    /// </summary>
    Task<int> GetScheduledCountAsync();

    /// <summary>
    /// Check if message is duplicate within time window
    /// </summary>
    Task<bool> IsDuplicateAsync(string phoneNumber, string message, int windowMinutes = 5);
}

/// <summary>
/// SMS statistics model
/// </summary>
public class SmsStatistics
{
    public int TotalQueued { get; set; }
    public int TotalSent { get; set; }
    public int TotalFailed { get; set; }
    public int TotalPending { get; set; }
    public int TotalProcessing { get; set; }
    public int TotalDelivered { get; set; }
    public double DeliverySuccessRate { get; set; }
    public Dictionary<string, int> ByMessageType { get; set; } = new();
    public Dictionary<string, int> ByProvider { get; set; } = new();
}
