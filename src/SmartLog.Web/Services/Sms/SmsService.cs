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
    private readonly ILogger<SmsService> _logger;

    public SmsService(
        ApplicationDbContext context,
        ISmsTemplateService templateService,
        ISmsSettingsService settingsService,
        ILogger<SmsService> logger)
    {
        _context = context;
        _templateService = templateService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task QueueAttendanceNotificationAsync(int studentId, string scanType, DateTime scanTime)
    {
        try
        {
            // Check if SMS is globally enabled
            if (!await _settingsService.IsSmsEnabledAsync())
            {
                _logger.LogDebug("SMS disabled globally, skipping notification");
                return;
            }

            // Get student with navigation properties
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

            // Determine template code based on scan type
            var templateCode = scanType.ToUpperInvariant() == "ENTRY" ? "ENTRY" : "EXIT";

            // Prepare placeholders
            var placeholders = new Dictionary<string, string>
            {
                { "StudentName", student.FullName },
                { "Grade", student.GradeLevel },
                { "Section", student.Section },
                { "Time", scanTime.ToLocalTime().ToString("h:mm tt") }
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

            _logger.LogInformation("Queued attendance notification for student {StudentId}", studentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing attendance notification for student {StudentId}", studentId);
        }
    }

    public async Task QueueCalendarEventNotificationsAsync(int calendarEventId)
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
            var placeholders = new Dictionary<string, string>
            {
                { "Date", calendarEvent.StartDate.ToString("MMM dd, yyyy") },
                { "EventTitle", calendarEvent.Title }
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
            }

            _logger.LogInformation("Queued calendar notifications for event {EventId}", calendarEventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing calendar notifications for event {EventId}", calendarEventId);
        }
    }

    public async Task QueueEmergencyAnnouncementAsync(
        string message,
        string? language = null,
        List<string>? affectedGrades = null)
    {
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

            // Use EMERGENCY template
            var placeholders = new Dictionary<string, string>
            {
                { "Message", message }
            };

            // Queue SMS for each student
            foreach (var student in students)
            {
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

                await QueueCustomSmsAsync(
                    student.ParentPhone,
                    renderedMessage,
                    SmsPriority.Emergency,
                    "EMERGENCY");
            }

            _logger.LogInformation("Queued emergency announcements for {Count} students", students.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing emergency announcements");
        }
    }

    public async Task<long> QueueCustomSmsAsync(
        string phoneNumber,
        string message,
        SmsPriority priority = SmsPriority.Normal,
        string messageType = "CUSTOM")
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
                CreatedAt = DateTime.UtcNow
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

    public async Task<SmsStatistics> GetStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var query = _context.SmsQueues.AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(q => q.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(q => q.CreatedAt <= endDate.Value);
            }

            var stats = new SmsStatistics
            {
                TotalQueued = await query.CountAsync(),
                TotalSent = await query.CountAsync(q => q.Status == SmsStatus.Sent),
                TotalFailed = await query.CountAsync(q => q.Status == SmsStatus.Failed),
                TotalPending = await query.CountAsync(q => q.Status == SmsStatus.Pending),
                TotalProcessing = await query.CountAsync(q => q.Status == SmsStatus.Processing),
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
