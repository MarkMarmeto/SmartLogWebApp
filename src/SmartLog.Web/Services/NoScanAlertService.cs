using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Services;

/// <summary>
/// Background service that runs daily at a configured time and sends an SMS alert
/// to parents of students who have no attendance scans for the day.
/// Implements US0052: End-of-Day No-Scan Alert.
/// Also implements INoScanAlertService for manual triggering from the admin dashboard.
/// </summary>
public class NoScanAlertService : BackgroundService, INoScanAlertService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NoScanAlertService> _logger;
    private readonly IConfiguration _configuration;

    public NoScanAlertService(
        IServiceProvider serviceProvider,
        ILogger<NoScanAlertService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NoScanAlertService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var alertTimeString = await GetAlertTimeStringAsync();
                var delay = CalculateDelayUntilAlertTime(alertTimeString);

                _logger.LogDebug("NoScanAlertService sleeping for {Delay} until next run", delay);
                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                {
                    // Double-processing guard: skip if already ran today (e.g. manually triggered earlier)
                    if (await HasRunTodayAsync(stoppingToken))
                    {
                        _logger.LogInformation("No-scan alert skipped by scheduler: already ran today");
                    }
                    else
                    {
                        await RunAlertCoreAsync(stoppingToken);
                    }
                }

                // Always sleep until tomorrow after the scheduled slot
                alertTimeString = await GetAlertTimeStringAsync();
                var untilTomorrow = CalculateDelayUntilTomorrow(alertTimeString);
                await Task.Delay(untilTomorrow, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in NoScanAlertService — will retry tomorrow");
                try
                {
                    var alertTimeString = await GetAlertTimeStringAsync();
                    await Task.Delay(CalculateDelayUntilTomorrow(alertTimeString), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("NoScanAlertService stopped");
    }

    /// <inheritdoc />
    public async Task<NoScanAlertTriggerResult> TriggerNowAsync(bool force = false, CancellationToken ct = default)
    {
        if (!force && await HasRunTodayAsync(ct))
            return new NoScanAlertTriggerResult(WasSkipped: true, Reason: "Already ran today");

        var queued = await RunAlertCoreAsync(ct);
        return new NoScanAlertTriggerResult(WasSkipped: false, Reason: "OK", AlertsQueued: queued);
    }

    /// <inheritdoc />
    public async Task<bool> HasRunTodayAsync(CancellationToken ct = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var todayUtc = DateTime.UtcNow.Date;
            return await context.AuditLogs
                .AnyAsync(a =>
                    (a.Action == "NO_SCAN_ALERT_EXECUTED" || a.Action == "NO_SCAN_ALERT_SUPPRESSED")
                    && a.Timestamp >= todayUtc,
                    ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check HasRunTodayAsync — assuming not run");
            return false;
        }
    }

    /// <summary>
    /// Reads the alert time from the database (AppSettings), falling back to appsettings.json,
    /// then to "18:10". Called on each loop iteration so admin changes apply without restart.
    /// </summary>
    private async Task<string> GetAlertTimeStringAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var appSettings = scope.ServiceProvider.GetRequiredService<IAppSettingsService>();
            var dbValue = await appSettings.GetAsync("Sms:NoScanAlertTime");
            if (!string.IsNullOrWhiteSpace(dbValue))
                return dbValue;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read Sms:NoScanAlertTime from database — falling back to config");
        }

        return _configuration.GetValue<string>("Sms:NoScanAlertTime") ?? "18:10";
    }

    /// <summary>
    /// Calculates the delay until the alert time today. If today's alert time has already passed,
    /// returns the delay until tomorrow's alert time. No catch-up window — use manual trigger instead.
    /// </summary>
    internal TimeSpan CalculateDelayUntilAlertTime(string alertTimeString)
    {
        var now = DateTime.Now;
        if (!TimeOnly.TryParse(alertTimeString, out var alertTime))
        {
            _logger.LogWarning("Invalid Sms:NoScanAlertTime value '{Value}' — defaulting to 18:10", alertTimeString);
            alertTime = new TimeOnly(18, 10);
        }

        var todayAlertDateTime = now.Date.Add(alertTime.ToTimeSpan());

        if (now < todayAlertDateTime)
            return todayAlertDateTime - now;

        // Already past today's alert time — wait until tomorrow
        return todayAlertDateTime.AddDays(1) - now;
    }

    /// <summary>
    /// Calculates delay until TOMORROW's alert time. Always returns a positive delay (≥ 1 second).
    /// Used after an alert has just fired to prevent same-day re-execution.
    /// </summary>
    internal TimeSpan CalculateDelayUntilTomorrow(string alertTimeString)
    {
        var now = DateTime.Now;
        if (!TimeOnly.TryParse(alertTimeString, out var alertTime))
            alertTime = new TimeOnly(18, 10);

        var tomorrowAlertDateTime = now.Date.AddDays(1).Add(alertTime.ToTimeSpan());
        var delay = tomorrowAlertDateTime - now;
        return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Core alert logic — queries students with no scans and queues SMS.
    /// Returns the number of alerts queued (new ones only, duplicates skipped).
    /// </summary>
    private async Task<int> RunAlertCoreAsync(CancellationToken stoppingToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var calendarService = scope.ServiceProvider.GetRequiredService<ICalendarService>();
        var templateService = scope.ServiceProvider.GetRequiredService<ISmsTemplateService>();
        var smsSettingsService = scope.ServiceProvider.GetRequiredService<ISmsSettingsService>();

        // Guard: SMS globally disabled
        if (!await smsSettingsService.IsSmsEnabledAsync())
        {
            _logger.LogInformation("No-scan alert skipped: SMS globally disabled");
            return 0;
        }

        // Guard: not a school day
        if (!await calendarService.IsSchoolDayAsync(DateTime.Today))
        {
            _logger.LogInformation("No-scan alert skipped: not a school day ({Date:yyyy-MM-dd})", DateTime.Today);
            return 0;
        }

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Guard: scanner health — if zero accepted scans today across all devices, suppress batch
        var totalScansToday = await context.Scans
            .CountAsync(s => s.Status == "ACCEPTED" && s.ScannedAt >= today && s.ScannedAt < tomorrow,
                stoppingToken);

        if (totalScansToday == 0)
        {
            _logger.LogWarning(
                "No-scan alert suppressed: zero total accepted scans today ({Date:yyyy-MM-dd}) — possible scanner issue",
                today);

            context.AuditLogs.Add(new AuditLog
            {
                Action = "NO_SCAN_ALERT_SUPPRESSED",
                Details = $"Date: {today:yyyy-MM-dd}. Suppressed: zero total scans today (possible scanner issue).",
                Timestamp = DateTime.UtcNow
            });
            await context.SaveChangesAsync(stoppingToken);
            return 0;
        }

        // Get current academic year
        var currentYear = await context.AcademicYears
            .FirstOrDefaultAsync(ay => ay.IsCurrent && ay.IsActive, stoppingToken);

        if (currentYear == null)
        {
            _logger.LogWarning("No-scan alert skipped: no active academic year found");
            return 0;
        }

        // Query students with no scans today: active + enrolled + SmsEnabled + ParentPhone set + no accepted scan today
        var studentsWithNoScans = await context.Students
            .Where(s => s.IsActive
                && s.SmsEnabled
                && !string.IsNullOrEmpty(s.ParentPhone)
                && context.StudentEnrollments.Any(se =>
                    se.StudentId == s.Id
                    && se.AcademicYearId == currentYear.Id
                    && se.IsActive)
                && !context.Scans.Any(sc =>
                    sc.StudentId == s.Id
                    && sc.Status == "ACCEPTED"
                    && sc.ScannedAt >= today
                    && sc.ScannedAt < tomorrow))
            .ToListAsync(stoppingToken);

        if (studentsWithNoScans.Count == 0)
        {
            _logger.LogInformation("No-scan alert: all students have scans today — 0 alerts queued");
            await WriteAuditLogAsync(context, today, 0, sw.ElapsedMilliseconds, stoppingToken);
            return 0;
        }

        // Get school settings for template rendering
        var appSettings = await context.AppSettings
            .Where(s => s.Key == "System.SchoolPhone" || s.Key == "System.SchoolName")
            .ToListAsync(stoppingToken);
        var schoolPhone = appSettings.FirstOrDefault(s => s.Key == "System.SchoolPhone")?.Value ?? string.Empty;
        var schoolName = appSettings.FirstOrDefault(s => s.Key == "System.SchoolName")?.Value ?? "School";

        var dateString = DateTime.Today.ToString("MMMM d, yyyy");
        int queued = 0;

        foreach (var student in studentsWithNoScans)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var placeholders = new Dictionary<string, string>
                {
                    { "StudentFirstName", student.FirstName },
                    { "GradeLevel", student.GradeLevel },
                    { "Section", student.Section },
                    { "Date", dateString },
                    { "SchoolPhone", schoolPhone },
                    { "SchoolName", schoolName }
                };

                var message = await templateService.RenderTemplateAsync(
                    "NO_SCAN_ALERT",
                    student.SmsLanguage,
                    placeholders);

                // Per-student idempotency: skip if already queued today for this student
                var isDuplicate = await context.SmsQueues
                    .AnyAsync(q =>
                        q.StudentId == student.Id
                        && q.MessageType == "NO_SCAN_ALERT"
                        && q.CreatedAt >= today
                        && q.Status != SmsStatus.Cancelled,
                        stoppingToken);

                if (isDuplicate)
                {
                    _logger.LogDebug("Skipped duplicate NO_SCAN_ALERT for {StudentId}", student.StudentId);
                    continue;
                }

                context.SmsQueues.Add(new SmsQueue
                {
                    PhoneNumber = student.ParentPhone,
                    Message = message,
                    Status = SmsStatus.Pending,
                    Priority = SmsPriority.Normal,
                    MessageType = "NO_SCAN_ALERT",
                    StudentId = student.Id,
                    RetryCount = 0,
                    MaxRetries = 3,
                    CreatedAt = DateTime.UtcNow
                });

                queued++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queuing NO_SCAN_ALERT for student {StudentId}", student.StudentId);
            }
        }

        await context.SaveChangesAsync(stoppingToken);

        sw.Stop();
        _logger.LogInformation(
            "No-scan alert executed: {Queued}/{Total} alerts queued in {Elapsed}ms ({Date:yyyy-MM-dd})",
            queued, studentsWithNoScans.Count, sw.ElapsedMilliseconds, today);

        await WriteAuditLogAsync(context, today, queued, sw.ElapsedMilliseconds, stoppingToken);
        return queued;
    }

    private static async Task WriteAuditLogAsync(
        ApplicationDbContext context,
        DateTime date,
        int alertsQueued,
        long elapsedMs,
        CancellationToken cancellationToken)
    {
        context.AuditLogs.Add(new AuditLog
        {
            Action = "NO_SCAN_ALERT_EXECUTED",
            Details = $"Date: {date:yyyy-MM-dd}. Alerts queued: {alertsQueued}. Duration: {elapsedMs}ms.",
            Timestamp = DateTime.UtcNow
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}
