using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class IndexModel : PageModel
{
    private readonly ISmsService _smsService;
    private readonly ApplicationDbContext _context;
    private readonly GsmModemGateway _gsmGateway;
    private readonly SemaphoreGateway _semaphoreGateway;
    private readonly INoScanAlertService _noScanAlertService;
    private readonly ISmsSettingsService _smsSettingsService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ISmsService smsService,
        ApplicationDbContext context,
        GsmModemGateway gsmGateway,
        SemaphoreGateway semaphoreGateway,
        INoScanAlertService noScanAlertService,
        ISmsSettingsService smsSettingsService,
        IAppSettingsService appSettingsService,
        ILogger<IndexModel> logger)
    {
        _smsService = smsService;
        _context = context;
        _gsmGateway = gsmGateway;
        _semaphoreGateway = semaphoreGateway;
        _noScanAlertService = noScanAlertService;
        _smsSettingsService = smsSettingsService;
        _appSettingsService = appSettingsService;
        _logger = logger;
    }

    public SmsStatistics Stats { get; set; } = new();
    public GatewayHealthStatus GsmHealth { get; set; } = new();
    public List<SmsLog> RecentLogs { get; set; } = new();
    public NoScanAlertRunStatus NoScanAlert { get; set; } = new();
    public bool NoScanAlertRanToday { get; set; }
    public bool IsSmsEnabled { get; set; }
    public string NextRunDisplay { get; set; } = "";

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        // Get statistics for last 7 days
        var startDate = DateTime.UtcNow.AddDays(-7);
        Stats = await _smsService.GetStatisticsAsync(startDate);

        // GSM health is local (no rate limit) — check on every page load
        GsmHealth = await _gsmGateway.GetHealthStatusAsync();
        // Semaphore health is on-demand via button (rate limited: 2 req/min)

        // Get recent logs (last 10)
        RecentLogs = await _context.SmsLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(10)
            .ToListAsync();

        // No-scan alert last run status
        var lastRun = await _context.AuditLogs
            .Where(a => a.Action == "NO_SCAN_ALERT_EXECUTED" || a.Action == "NO_SCAN_ALERT_SUPPRESSED")
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();
        NoScanAlert = NoScanAlertRunStatus.FromAuditLog(lastRun);
        NoScanAlertRanToday = await _noScanAlertService.HasRunTodayAsync();

        // Next Run display
        IsSmsEnabled = await _smsSettingsService.IsSmsEnabledAsync();
        var alertTimeStr = await _appSettingsService.GetAsync("Sms:NoScanAlertTime") ?? "18:10";
        NextRunDisplay = ComputeNextRunDisplay(IsSmsEnabled, NoScanAlertRanToday, alertTimeStr);
    }

    internal static string ComputeNextRunDisplay(bool isSmsEnabled, bool ranToday, string alertTimeStr)
    {
        if (!isSmsEnabled)
            return "Disabled";

        if (!TimeOnly.TryParse(alertTimeStr, out var alertTime))
            alertTime = new TimeOnly(18, 10);

        var timeFormatted = alertTime.ToString("h:mm tt");
        return ranToday
            ? $"Tomorrow at {timeFormatted}"
            : $"Today at {timeFormatted}";
    }

    /// <summary>
    /// POST ?handler=TriggerNoScanAlert — manually runs the no-scan alert for today.
    /// Accepts force=true to re-run even if it already ran.
    /// </summary>
    public async Task<IActionResult> OnPostTriggerNoScanAlertAsync(bool force = false)
    {
        try
        {
            var result = await _noScanAlertService.TriggerNowAsync(force);
            StatusMessage = result.WasSkipped
                ? $"Skipped: {result.Reason}"
                : $"Done — {result.AlertsQueued} alert(s) queued.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual no-scan alert trigger failed");
            StatusMessage = "Error: Could not trigger no-scan alert. Check logs.";
        }

        return RedirectToPage();
    }

    /// <summary>
    /// GET ?handler=SemaphoreStatus — called on demand by the Check Status button.
    /// Returns GatewayHealthStatus as JSON. Rate limit: 2 req/min on Semaphore's side.
    /// </summary>
    public async Task<IActionResult> OnGetSemaphoreStatusAsync()
    {
        try
        {
            var health = await _semaphoreGateway.GetHealthStatusAsync();
            return new JsonResult(new
            {
                isHealthy = health.IsHealthy,
                status = health.Status,
                details = health.Details,
                checkedAt = DateTime.Now.ToString("MMM d, yyyy h:mm:ss tt")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Semaphore status");
            return new JsonResult(new
            {
                isHealthy = false,
                status = "Error",
                details = new Dictionary<string, string> { { "Error", ex.Message } },
                checkedAt = DateTime.Now.ToString("MMM d, yyyy h:mm:ss tt")
            });
        }
    }
}

public class NoScanAlertRunStatus
{
    public bool HasRun { get; init; }
    public bool WasSuppressed { get; init; }
    public DateTime? RunAt { get; init; }
    public int AlertsQueued { get; init; }

    public static NoScanAlertRunStatus FromAuditLog(AuditLog? log)
    {
        if (log == null) return new NoScanAlertRunStatus();

        var suppressed = log.Action == "NO_SCAN_ALERT_SUPPRESSED";
        var count = 0;
        if (!suppressed && log.Details != null)
        {
            var match = Regex.Match(log.Details, @"Alerts queued: (\d+)");
            if (match.Success) int.TryParse(match.Groups[1].Value, out count);
        }

        return new NoScanAlertRunStatus
        {
            HasRun = true,
            WasSuppressed = suppressed,
            RunAt = log.Timestamp,
            AlertsQueued = count
        };
    }
}
