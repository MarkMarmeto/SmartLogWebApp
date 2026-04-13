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
    private readonly SemaphoreGateway? _semaphoreGateway;
    private readonly GsmModemGateway? _gsmGateway;
    private readonly ILogger<SmsService> _logger;

    public SmsService(
        ApplicationDbContext context,
        ISmsTemplateService templateService,
        ISmsSettingsService settingsService,
        IAppSettingsService appSettingsService,
        ILogger<SmsService> logger,
        SemaphoreGateway? semaphoreGateway = null,
        GsmModemGateway? gsmGateway = null)
    {
        _context = context;
        _templateService = templateService;
        _settingsService = settingsService;
        _appSettingsService = appSettingsService;
        _semaphoreGateway = semaphoreGateway;
        _gsmGateway = gsmGateway;
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
            var query = _context.Students.Where(s => s.IsActive && s.SmsEnabled);
            if (affectedGrades != null && affectedGrades.Any())
                query = query.Where(s => affectedGrades.Contains(s.GradeLevel));
            var students = await query.ToListAsync();

            _logger.LogInformation("Sending emergency announcement to {Count} students", students.Count);

            var broadcast = new Data.Entities.Broadcast
            {
                Id = broadcastId,
                Type = "EMERGENCY",
                Message = message,
                Language = language,
                AffectedGrades = affectedGrades != null && affectedGrades.Any()
                    ? System.Text.Json.JsonSerializer.Serialize(affectedGrades) : null,
                ScheduledAt = null,
                Status = Data.Entities.BroadcastStatus.Sending,
                CreatedByUserId = createdByUserId,
                CreatedByName = createdByName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Broadcasts.Add(broadcast);
            await _context.SaveChangesAsync();

            var schoolPhone = await _appSettingsService.GetAsync("System.SchoolPhone") ?? "";
            var placeholders = new Dictionary<string, string>
            {
                { "Message", message },
                { "SchoolPhone", schoolPhone }
            };

            var recipientCount = await ExecuteBroadcastAsync(
                broadcast, students, "EMERGENCY", placeholders,
                SmsPriority.Emergency, scheduledAt: null, checkDuplicates: false);

            broadcast.TotalRecipients = recipientCount;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Emergency broadcast {BroadcastId}: {Recipients} messages dispatched",
                broadcastId, recipientCount);

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

            var isScheduled = scheduledAt.HasValue && scheduledAt.Value > DateTime.UtcNow;
            var broadcast = new Data.Entities.Broadcast
            {
                Id = broadcastId,
                Type = "ANNOUNCEMENT",
                Message = message,
                Language = language,
                AffectedGrades = affectedGrades != null && affectedGrades.Any()
                    ? System.Text.Json.JsonSerializer.Serialize(affectedGrades) : null,
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

            var recipientCount = await ExecuteBroadcastAsync(
                broadcast, students, "ANNOUNCEMENT", placeholders,
                SmsPriority.High, scheduledAt, checkDuplicates: true);

            broadcast.TotalRecipients = recipientCount;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Announcement broadcast {BroadcastId}: {Recipients} messages dispatched",
                broadcastId, recipientCount);

            return broadcastId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing announcements");
            return broadcastId;
        }
    }

    /// <summary>
    /// Core broadcast dispatch: uses Semaphore bulk API when available and the send is immediate;
    /// falls back to individual queue rows (GSM modem path or scheduled announcements).
    ///
    /// Scalability fixes for large schools (5,000+ students):
    /// 1. Template is fetched ONCE per language variant, not per student.
    /// 2. Duplicate check is a single batch query against all phones at once.
    /// 3. DB inserts are batched — single SaveChangesAsync for all rows.
    /// </summary>
    private async Task<int> ExecuteBroadcastAsync(
        Data.Entities.Broadcast broadcast,
        List<Student> students,
        string templateCode,
        Dictionary<string, string> placeholders,
        SmsPriority priority,
        DateTime? scheduledAt,
        bool checkDuplicates)
    {
        var broadcastId = broadcast.Id;
        var messageType = broadcast.Type;
        var language = broadcast.Language;

        var isScheduled = scheduledAt.HasValue && scheduledAt.Value > DateTime.UtcNow;
        var useBulk = !isScheduled && await CanUseBulkAsync();

        // Pre-render each language variant ONCE — needed before the duplicate check
        // so we can match on exact message content (prevents different broadcasts blocking each other).
        var renderedByLanguage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lang in students.Select(s => language ?? s.SmsLanguage).Distinct())
        {
            var rendered = await _templateService.RenderTemplateAsync(templateCode, lang, placeholders);
            if (!string.IsNullOrWhiteSpace(rendered))
                renderedByLanguage[lang] = rendered;
        }

        // Batch duplicate check — one query per rendered message variant.
        // Matching on exact message content means two different announcements sent within the
        // 60-minute window won't block each other (only the same text to the same phone is a duplicate).
        HashSet<string>? duplicatePhones = null;
        if (checkDuplicates)
        {
            var allCandidatePhones = students
                .SelectMany(s => GetPhonesForStudent(s))
                .ToList();

            var cutoff = DateTime.UtcNow.AddMinutes(-60);
            duplicatePhones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var renderedMessage in renderedByLanguage.Values.Distinct(StringComparer.Ordinal))
            {
                var dupes = await _context.SmsQueues
                    .Where(q => allCandidatePhones.Contains(q.PhoneNumber) &&
                                q.Message == renderedMessage &&
                                q.CreatedAt >= cutoff &&
                                q.Status != SmsStatus.Cancelled &&
                                q.MessageType == messageType)
                    .Select(q => q.PhoneNumber)
                    .Distinct()
                    .ToListAsync();

                foreach (var phone in dupes)
                    duplicatePhones.Add(phone);
            }
        }

        // Group recipients by their rendered message (EN vs FIL)
        var groups = new Dictionary<string, List<string>>();

        foreach (var student in students)
        {
            if (string.IsNullOrWhiteSpace(student.ParentPhone)) continue;

            var smsLanguage = language ?? student.SmsLanguage;

            if (!renderedByLanguage.TryGetValue(smsLanguage, out var renderedMessage)) continue;

            if (string.IsNullOrWhiteSpace(renderedMessage)) continue;

            foreach (var phone in GetPhonesForStudent(student))
            {
                if (duplicatePhones != null && duplicatePhones.Contains(phone)) continue;

                if (!groups.TryGetValue(renderedMessage, out var list))
                {
                    list = new List<string>();
                    groups[renderedMessage] = list;
                }
                list.Add(phone);
            }
        }

        int recipientCount = 0;

        if (useBulk)
        {
            recipientCount = await SendBulkGroupsAsync(groups, broadcastId, messageType, priority);

            // Finalize broadcast status: bulk path sends immediately (no worker involvement),
            // so we must update Broadcast.Status here after all messages are processed.
            await FinalizeBroadcastAsync(broadcastId);
        }
        else
        {
            // FIX 3: Batch queue inserts for GSM modem / scheduled path
            recipientCount = await QueueBroadcastBatchAsync(
                groups, broadcastId, messageType, priority, scheduledAt);
        }

        return recipientCount;
    }

    private static IEnumerable<string> GetPhonesForStudent(Student student)
    {
        if (!string.IsNullOrWhiteSpace(student.ParentPhone))
            yield return student.ParentPhone;
        if (!string.IsNullOrWhiteSpace(student.AlternatePhone))
            yield return student.AlternatePhone;
    }

    /// <summary>
    /// Converts a Philippine mobile number to the international format Semaphore echoes back.
    /// 09XXXXXXXXX → 639XXXXXXXXX, +639XXXXXXXXX → 639XXXXXXXXX
    /// Used only for matching Semaphore bulk-send results back to raw-format phone numbers.
    /// Stored phone numbers are kept in their original format.
    /// </summary>
    private static string NormalizePhoneForLookup(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("09") && digits.Length == 11)
            return "63" + digits[1..];
        if (phoneNumber.StartsWith("+"))
            return digits;
        return digits;
    }

    /// <summary>
    /// Returns true when Semaphore is the configured default provider and is reachable.
    /// </summary>
    private async Task<bool> CanUseBulkAsync()
    {
        if (_semaphoreGateway == null) return false;

        var provider = await _settingsService.GetSettingAsync("Sms.DefaultProvider") ?? "SEMAPHORE";
        if (!provider.Equals("SEMAPHORE", StringComparison.OrdinalIgnoreCase)) return false;

        return await _semaphoreGateway.IsAvailableAsync();
    }

    /// <summary>
    /// Marks a broadcast as Sent after all messages have been processed.
    /// Called after the bulk send path, which bypasses the SmsWorkerService.
    /// </summary>
    private async Task FinalizeBroadcastAsync(Guid broadcastId)
    {
        try
        {
            var broadcast = await _context.Broadcasts
                .FirstOrDefaultAsync(b => b.Id == broadcastId);

            if (broadcast == null || broadcast.Status == Data.Entities.BroadcastStatus.Cancelled)
                return;

            // Check that no messages are still pending (shouldn't happen after bulk, but be safe)
            var stillPending = await _context.SmsQueues
                .AnyAsync(q => q.BroadcastId == broadcastId &&
                               (q.Status == SmsStatus.Pending || q.Status == SmsStatus.Processing));

            if (stillPending) return;

            broadcast.Status = Data.Entities.BroadcastStatus.Sent;
            broadcast.SentAt = DateTime.UtcNow;
            broadcast.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Broadcast {BroadcastId} finalized to Sent after bulk send", broadcastId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing broadcast {BroadcastId} after bulk send", broadcastId);
        }
    }

    /// <summary>
    /// Sends each message group via Semaphore bulk API, then writes all SmsQueue + SmsLog rows
    /// in a single SaveChangesAsync call (batched, not per-recipient).
    /// </summary>
    private async Task<int> SendBulkGroupsAsync(
        Dictionary<string, List<string>> groups,
        Guid broadcastId,
        string messageType,
        SmsPriority priority)
    {
        int total = 0;

        foreach (var (renderedMessage, phones) in groups)
        {
            var bulkResult = await _semaphoreGateway!.SendBulkAsync(phones, renderedMessage);
            var messageParts = CalculateMessageParts(renderedMessage);
            var now = DateTime.UtcNow;

            // Semaphore returns normalized phone numbers (639XXXXXXXXX).
            // phones list may have raw format (09XXXXXXXXX). Build lookup with both forms
            // so the outcome is found regardless of which format Semaphore echoes back.
            var outcomeByPhone = bulkResult.Results
                .ToDictionary(r => r.PhoneNumber, r => r, StringComparer.OrdinalIgnoreCase);

            // FIX: collect all queue entries first, then save once
            var queueEntries = phones.Select(phone =>
            {
                // Try raw first, then normalized (Semaphore echoes back 639XXXXXXXXX)
                if (!outcomeByPhone.TryGetValue(phone, out var outcome))
                    outcomeByPhone.TryGetValue(NormalizePhoneForLookup(phone), out outcome);
                var success = outcome?.Success ?? false;
                return new SmsQueue
                {
                    PhoneNumber = phone,
                    Message = renderedMessage,
                    Status = success ? SmsStatus.Sent : SmsStatus.Failed,
                    Priority = priority,
                    MessageType = messageType,
                    RetryCount = success ? 0 : 3,
                    MaxRetries = 3,
                    CreatedAt = now,
                    ProcessedAt = now,
                    SentAt = success ? now : null,
                    Provider = "SEMAPHORE",
                    ProviderMessageId = outcome?.ProviderMessageId,
                    ErrorMessage = outcome?.ErrorMessage,
                    BroadcastId = broadcastId
                };
            }).ToList();

            _context.SmsQueues.AddRange(queueEntries);
            await _context.SaveChangesAsync(); // single save for all queue entries

            // Now write SmsLog rows (need queue IDs, which are assigned after save)
            var logEntries = queueEntries.Select(q =>
            {
                outcomeByPhone.TryGetValue(q.PhoneNumber, out var outcome);
                return new SmsLog
                {
                    QueueId = q.Id,
                    PhoneNumber = q.PhoneNumber,
                    Message = renderedMessage,
                    Status = q.Status == SmsStatus.Sent ? "SENT" : "FAILED",
                    Provider = "SEMAPHORE",
                    ProviderMessageId = q.ProviderMessageId,
                    ErrorMessage = q.ErrorMessage,
                    MessageParts = messageParts,
                    ProcessingTimeMs = bulkResult.ProcessingTimeMs,
                    CreatedAt = now,
                    SentAt = q.SentAt
                };
            }).ToList();

            _context.SmsLogs.AddRange(logEntries);
            await _context.SaveChangesAsync(); // single save for all log entries

            total += bulkResult.SuccessCount;

            _logger.LogInformation(
                "Bulk broadcast group: {Success}/{Total} sent via Semaphore in {Ms}ms",
                bulkResult.SuccessCount, phones.Count, bulkResult.ProcessingTimeMs);
        }

        return total;
    }

    /// <summary>
    /// Inserts all broadcast queue rows in a single batch (GSM modem / scheduled path).
    /// </summary>
    private async Task<int> QueueBroadcastBatchAsync(
        Dictionary<string, List<string>> groups,
        Guid broadcastId,
        string messageType,
        SmsPriority priority,
        DateTime? scheduledAt)
    {
        var now = DateTime.UtcNow;
        var entries = groups
            .SelectMany(kvp => kvp.Value.Select(phone => new SmsQueue
            {
                PhoneNumber = phone,
                Message = kvp.Key,
                Status = SmsStatus.Pending,
                Priority = priority,
                MessageType = messageType,
                RetryCount = 0,
                MaxRetries = 3,
                CreatedAt = now,
                ScheduledAt = scheduledAt,
                BroadcastId = broadcastId
            }))
            .ToList();

        _context.SmsQueues.AddRange(entries);
        await _context.SaveChangesAsync();

        return entries.Count;
    }

    private static int CalculateMessageParts(string message)
    {
        if (message.Length <= 160) return 1;
        return (int)Math.Ceiling((double)message.Length / 153);
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
