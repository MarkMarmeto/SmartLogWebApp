using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services.Sms;

/// <summary>
/// Core SMS service for queuing and managing notifications
/// </summary>
public class SmsService : ISmsService
{
    private readonly ApplicationDbContext _context;
    private readonly ISmsTemplateService _templateService;
    private readonly ISmsSettingsService _settingsService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly ILogger<SmsService> _logger;

    public SmsService(
        ApplicationDbContext context,
        ISmsTemplateService templateService,
        ISmsSettingsService settingsService,
        IAppSettingsService appSettingsService,
        ILogger<SmsService> logger)
    {
        _context = context;
        _templateService = templateService;
        _settingsService = settingsService;
        _appSettingsService = appSettingsService;
        _logger = logger;
    }

    public async Task QueueAttendanceNotificationAsync(Guid studentId, string scanType, DateTime scanTime, Guid scanId)
    {
        try
        {
            // Check if SMS is globally enabled
            if (!await _settingsService.IsSmsEnabledAsync())
            {
                _logger.LogDebug("SMS disabled globally, skipping notification");
                return;
            }

            // Get student
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
            {
                _logger.LogWarning("Student not found: {StudentId}", studentId);
                return;
            }

            // Check if SMS enabled for this student
            if (!student.SmsEnabled)
            {
                _logger.LogDebug("SMS disabled for student {StudentId}", studentId);
                return;
            }

            // Guard: skip if parent phone is missing or invalid
            if (string.IsNullOrWhiteSpace(student.ParentPhone))
            {
                _logger.LogWarning("Student {StudentId} has no parent phone — skipping SMS", studentId);
                return;
            }

            // Determine template code based on scan type
            var templateCode = scanType.ToUpperInvariant() == "ENTRY" ? "ENTRY" : "EXIT";

            // Fetch school phone — data-minimized: first name only, no grade/section
            var schoolPhone = await _appSettingsService.GetAsync("System.SchoolPhone") ?? "";
            var localTime = scanTime.ToLocalTime();

            // Compliant placeholders (RA 10173 / data minimization):
            // - First name only (not full name)
            // - No grade/section (unnecessary for the notification purpose)
            // - ScanRef: first 8 chars of scan Guid for parent verification
            var placeholders = new Dictionary<string, string>
            {
                { "StudentFirstName", student.FirstName },
                { "Date", localTime.ToString("MMM d, yyyy") },
                { "Time", localTime.ToString("h:mm tt") },
                { "SchoolPhone", schoolPhone },
                { "ScanRef", scanId.ToString("N")[..8].ToUpperInvariant() }
            };

            // Render template
            var message = await _templateService.RenderTemplateAsync(
                templateCode,
                student.SmsLanguage,
                placeholders);

            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogError("Failed to render template {Code} for student {StudentId}",
                    templateCode, studentId);
                return;
            }

            // Check for duplicates (5 minute window)
            if (await IsDuplicateAsync(student.ParentPhone, message, 5))
            {
                _logger.LogDebug("Duplicate SMS detected for {Phone}, skipping", student.ParentPhone);
                return;
            }

            // Queue the SMS
            await QueueCustomSmsAsync(
                student.ParentPhone,
                message,
                SmsPriority.Normal,
                "ATTENDANCE");

            // Also notify alternate phone if present
            if (!string.IsNullOrWhiteSpace(student.AlternatePhone) &&
                !await IsDuplicateAsync(student.AlternatePhone, message, 5))
            {
                await QueueCustomSmsAsync(
                    student.AlternatePhone,
                    message,
                    SmsPriority.Normal,
                    "ATTENDANCE");
            }

            _logger.LogInformation("Queued attendance notification for student {StudentId}", studentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing attendance notification for student {StudentId}", studentId);
        }
    }

    public async Task QueueCalendarEventNotificationsAsync(Guid calendarEventId)
    {
        try
        {
            // Check if SMS is globally enabled
            if (!await _settingsService.IsSmsEnabledAsync())
            {
                _logger.LogDebug("SMS disabled globally, skipping calendar notifications");
                return;
            }

            // Get calendar event
            var calendarEvent = await _context.CalendarEvents
                .FirstOrDefaultAsync(e => e.Id == calendarEventId);

            if (calendarEvent == null)
            {
                _logger.LogWarning("Calendar event not found: {EventId}", calendarEventId);
                return;
            }

            // Determine template code based on event type
            string templateCode;
            if (calendarEvent.EventType == EventType.Holiday)
            {
                templateCode = "HOLIDAY";
            }
            else if (calendarEvent.EventType == EventType.Suspension)
            {
                templateCode = "SUSPENSION";
            }
            else
            {
                _logger.LogDebug("Event type {Type} does not require SMS notification", calendarEvent.EventType);
                return;
            }

            // Get all active students with SMS enabled
            var students = await _context.Students
                .Where(s => s.IsActive && s.SmsEnabled)
                .ToListAsync();

            _logger.LogInformation("Sending calendar notification to {Count} students", students.Count);

            // Prepare placeholders
            var schoolPhone = await _appSettingsService.GetAsync("System.SchoolPhone") ?? "";
            var placeholders = new Dictionary<string, string>
            {
                { "Date", calendarEvent.StartDate.ToString("MMM d, yyyy") },
                { "EventTitle", calendarEvent.Title },
                { "SchoolPhone", schoolPhone }
            };

            // Queue SMS for each student
            foreach (var student in students)
            {
                var message = await _templateService.RenderTemplateAsync(
                    templateCode,
                    student.SmsLanguage,
                    placeholders);

                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                // Check for duplicates
                if (await IsDuplicateAsync(student.ParentPhone, message, 60))
                {
                    continue;
                }

                await QueueCustomSmsAsync(
                    student.ParentPhone,
                    message,
                    SmsPriority.High,
                    "CALENDAR");

                if (!string.IsNullOrWhiteSpace(student.AlternatePhone) &&
                    !await IsDuplicateAsync(student.AlternatePhone, message, 60))
                {
                    await QueueCustomSmsAsync(
                        student.AlternatePhone,
                        message,
                        SmsPriority.High,
                        "CALENDAR");
                }
            }

            _logger.LogInformation("Queued calendar notifications for event {EventId}", calendarEventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing calendar notifications for event {EventId}", calendarEventId);
        }
    }

    public async Task<Guid> QueueEmergencyAnnouncementAsync(
        string message,
        string? language = null,
        List<string>? affectedGrades = null,
        string? createdByUserId = null,
        string? createdByName = null)
    {
        var broadcastId = Guid.NewGuid();
        try
        {
            // Get students filtered by grade if specified
            var query = _context.Students.Where(s => s.IsActive && s.SmsEnabled);

            if (affectedGrades != null && affectedGrades.Any())
            {
                query = query.Where(s => affectedGrades.Contains(s.GradeLevel));
            }

            var students = await query.ToListAsync();

            _logger.LogInformation("Sending emergency announcement to {Count} students", students.Count);

            // Create Broadcast record
            var broadcast = new Data.Entities.Broadcast
            {
                Id = broadcastId,
                Type = "EMERGENCY",
                Message = message,
                Language = language,
                AffectedGrades = affectedGrades != null && affectedGrades.Any()
                    ? System.Text.Json.JsonSerializer.Serialize(affectedGrades)
                    : null,
                ScheduledAt = null,
                Status = Data.Entities.BroadcastStatus.Sending,
                CreatedByUserId = createdByUserId,
                CreatedByName = createdByName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Broadcasts.Add(broadcast);
            await _context.SaveChangesAsync();

            // Use EMERGENCY template
            var schoolPhone = await _appSettingsService.GetAsync("System.SchoolPhone") ?? "";
            var placeholders = new Dictionary<string, string>
            {
                { "Message", message },
                { "SchoolPhone", schoolPhone }
            };

            int recipientCount = 0;

            // Queue SMS for each student
            foreach (var student in students)
            {
                if (string.IsNullOrWhiteSpace(student.ParentPhone)) continue;

                // Use specified language or student's preference
                var smsLanguage = language ?? student.SmsLanguage;

                var renderedMessage = await _templateService.RenderTemplateAsync(
                    "EMERGENCY",
                    smsLanguage,
                    placeholders);

                if (string.IsNullOrWhiteSpace(renderedMessage))
                {
                    continue;
                }

                await QueueSmsInternalAsync(
                    student.ParentPhone,
                    renderedMessage,
                    SmsPriority.Emergency,
                    "EMERGENCY",
                    scheduledAt: null,
                    broadcastId: broadcastId);
                recipientCount++;

                if (!string.IsNullOrWhiteSpace(student.AlternatePhone))
                {
                    await QueueSmsInternalAsync(
                        student.AlternatePhone,
                        renderedMessage,
                        SmsPriority.Emergency,
                        "EMERGENCY",
                        scheduledAt: null,
                        broadcastId: broadcastId);
                    recipientCount++;
                }
            }

            // Update broadcast with final count and status
            broadcast.TotalRecipients = recipientCount;
            broadcast.Status = Data.Entities.BroadcastStatus.Sending;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Queued emergency announcements for {Count} students ({Recipients} messages)",
                students.Count, recipientCount);

            return broadcastId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing emergency announcements");
            return broadcastId;
        }
    }

    public async Task<Guid> QueueAnnouncementAsync(
        string message,
        string? language = null,
        List<string>? affectedGrades = null,
        DateTime? scheduledAt = null,
        string? createdByUserId = null,
        string? createdByName = null)
    {
        var broadcastId = Guid.NewGuid();
        try
        {
            var query = _context.Students.Where(s => s.IsActive && s.SmsEnabled);

            if (affectedGrades != null && affectedGrades.Any())
                query = query.Where(s => affectedGrades.Contains(s.GradeLevel));

            var students = await query.ToListAsync();

            _logger.LogInformation("Sending announcement to {Count} students", students.Count);

            // Create Broadcast record
            var isScheduled = scheduledAt.HasValue && scheduledAt.Value > DateTime.UtcNow;
            var broadcast = new Data.Entities.Broadcast
            {
                Id = broadcastId,
                Type = "ANNOUNCEMENT",
                Message = message,
                Language = language,
                AffectedGrades = affectedGrades != null && affectedGrades.Any()
                    ? System.Text.Json.JsonSerializer.Serialize(affectedGrades)
                    : null,
                ScheduledAt = scheduledAt,
                Status = isScheduled ? Data.Entities.BroadcastStatus.Scheduled : Data.Entities.BroadcastStatus.Sending,
                CreatedByUserId = createdByUserId,
                CreatedByName = createdByName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Broadcasts.Add(broadcast);
            await _context.SaveChangesAsync();

            var schoolPhone = await _appSettingsService.GetAsync("System.SchoolPhone") ?? "";
            var schoolName = await _appSettingsService.GetAsync("System.SchoolName") ?? "School";

            var placeholders = new Dictionary<string, string>
            {
                { "SchoolName", schoolName },
                { "Message", message },
                { "SchoolPhone", schoolPhone }
            };

            int recipientCount = 0;

            foreach (var student in students)
            {
                if (string.IsNullOrWhiteSpace(student.ParentPhone)) continue;

                var smsLanguage = language ?? student.SmsLanguage;

                var renderedMessage = await _templateService.RenderTemplateAsync(
                    "ANNOUNCEMENT",
                    smsLanguage,
                    placeholders);

                if (string.IsNullOrWhiteSpace(renderedMessage))
                    continue;

                if (await IsDuplicateAsync(student.ParentPhone, renderedMessage, 60))
                    continue;

                await QueueSmsInternalAsync(
                    student.ParentPhone,
                    renderedMessage,
                    SmsPriority.High,
                    "ANNOUNCEMENT",
                    scheduledAt: scheduledAt,
                    broadcastId: broadcastId);
                recipientCount++;

                if (!string.IsNullOrWhiteSpace(student.AlternatePhone) &&
                    !await IsDuplicateAsync(student.AlternatePhone, renderedMessage, 60))
                {
                    await QueueSmsInternalAsync(
                        student.AlternatePhone,
                        renderedMessage,
                        SmsPriority.High,
                        "ANNOUNCEMENT",
                        scheduledAt: scheduledAt,
                        broadcastId: broadcastId);
                    recipientCount++;
                }
            }

            // Update broadcast with final count
            broadcast.TotalRecipients = recipientCount;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Queued announcements for {Count} students ({Recipients} messages)",
                students.Count, recipientCount);

            return broadcastId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing announcements");
            return broadcastId;
        }
    }

    public Task<long> QueueCustomSmsAsync(
        string phoneNumber,
        string message,
        SmsPriority priority = SmsPriority.Normal,
        string messageType = "CUSTOM",
        DateTime? scheduledAt = null)
        => QueueSmsInternalAsync(phoneNumber, message, priority, messageType, scheduledAt, broadcastId: null);

    private async Task<long> QueueSmsInternalAsync(
        string phoneNumber,
        string message,
        SmsPriority priority,
        string messageType,
        DateTime? scheduledAt,
        Guid? broadcastId)
    {
        try
        {
            var queueEntry = new SmsQueue
            {
                PhoneNumber = phoneNumber,
                Message = message,
                Status = SmsStatus.Pending,
                Priority = priority,
                MessageType = messageType,
                RetryCount = 0,
                MaxRetries = 3,
                CreatedAt = DateTime.UtcNow,
                ScheduledAt = scheduledAt,
                BroadcastId = broadcastId
            };

            _context.SmsQueues.Add(queueEntry);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Queued SMS {Id} to {Phone}", queueEntry.Id, phoneNumber);

            return queueEntry.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing custom SMS to {Phone}", phoneNumber);
            throw;
        }
    }

    public async Task<int> CancelBroadcastAsync(Guid broadcastId)
    {
        try
        {
            var broadcast = await _context.Broadcasts
                .FirstOrDefaultAsync(b => b.Id == broadcastId);

            if (broadcast == null)
            {
                return 0;
            }

            // Cancel all pending messages in this broadcast
            var pending = await _context.SmsQueues
                .Where(q => q.BroadcastId == broadcastId &&
                            (q.Status == SmsStatus.Pending || q.Status == SmsStatus.Failed))
                .ToListAsync();

            foreach (var msg in pending)
            {
                msg.Status = SmsStatus.Cancelled;
            }

            broadcast.Status = Data.Entities.BroadcastStatus.Cancelled;
            broadcast.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Cancelled broadcast {BroadcastId} — {Count} message(s) cancelled",
                broadcastId, pending.Count);

            return pending.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling broadcast {BroadcastId}", broadcastId);
            return 0;
        }
    }

    public async Task<List<Data.Entities.Broadcast>> GetBroadcastsAsync(int page = 1, int pageSize = 20)
    {
        try
        {
            return await _context.Broadcasts
                .OrderByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting broadcasts");
            return new List<Data.Entities.Broadcast>();
        }
    }

    public async Task<Data.Entities.Broadcast?> GetBroadcastAsync(Guid broadcastId)
    {
        try
        {
            return await _context.Broadcasts
                .FirstOrDefaultAsync(b => b.Id == broadcastId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting broadcast {BroadcastId}", broadcastId);
            return null;
        }
    }

    public async Task<bool> CancelSmsAsync(long queueId)
    {
        try
        {
            var queueEntry = await _context.SmsQueues
                .FirstOrDefaultAsync(q => q.Id == queueId);

            if (queueEntry == null)
            {
                return false;
            }

            // Only cancel if pending or failed
            if (queueEntry.Status == SmsStatus.Pending ||
                queueEntry.Status == SmsStatus.Failed)
            {
                queueEntry.Status = SmsStatus.Cancelled;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cancelled SMS {Id}", queueId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling SMS {Id}", queueId);
            return false;
        }
    }

    public async Task<int> GetScheduledCountAsync()
    {
        try
        {
            return await _context.SmsQueues
                .CountAsync(q => q.Status == SmsStatus.Pending && q.ScheduledAt > DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scheduled SMS count");
            return 0;
        }
    }

    public async Task<SmsStatistics> GetStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var query = _context.SmsQueues
                .Where(q => q.MessageType != "TEST")
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(q => q.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(q => q.CreatedAt <= endDate.Value);
            }

            var totalSent = await query.CountAsync(q => q.Status == SmsStatus.Sent);

            // Query delivery stats from SmsLog
            var logQuery = _context.SmsLogs
                .Where(l => l.Status == "SENT");

            if (startDate.HasValue)
            {
                logQuery = logQuery.Where(l => l.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                logQuery = logQuery.Where(l => l.CreatedAt <= endDate.Value);
            }

            var totalDelivered = await logQuery.CountAsync(l => l.DeliveryStatus == "DELIVERED");

            var stats = new SmsStatistics
            {
                TotalQueued = await query.CountAsync(),
                TotalSent = totalSent,
                TotalFailed = await query.CountAsync(q => q.Status == SmsStatus.Failed),
                TotalPending = await query.CountAsync(q => q.Status == SmsStatus.Pending),
                TotalProcessing = await query.CountAsync(q => q.Status == SmsStatus.Processing),
                TotalDelivered = totalDelivered,
                DeliverySuccessRate = totalSent > 0 ? Math.Round((double)totalDelivered / totalSent * 100, 1) : 0,
                ByMessageType = await query
                    .GroupBy(q => q.MessageType)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Type, x => x.Count),
                ByProvider = await query
                    .Where(q => q.Provider != null)
                    .GroupBy(q => q.Provider!)
                    .Select(g => new { Provider = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Provider, x => x.Count)
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SMS statistics");
            return new SmsStatistics();
        }
    }

    public async Task<bool> IsDuplicateAsync(string phoneNumber, string message, int windowMinutes = 5)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-windowMinutes);

            var duplicate = await _context.SmsQueues
                .AnyAsync(q =>
                    q.PhoneNumber == phoneNumber &&
                    q.Message == message &&
                    q.CreatedAt >= cutoffTime &&
                    q.Status != SmsStatus.Cancelled);

            return duplicate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for duplicate SMS");
            return false;
        }
    }
}
