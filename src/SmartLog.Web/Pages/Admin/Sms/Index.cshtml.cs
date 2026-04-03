using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class IndexModel : PageModel
{
    private readonly ISmsService _smsService;
    private readonly ApplicationDbContext _context;
    private readonly GsmModemGateway _gsmGateway;
    private readonly SemaphoreGateway _semaphoreGateway;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ISmsService smsService,
        ApplicationDbContext context,
        GsmModemGateway gsmGateway,
        SemaphoreGateway semaphoreGateway,
        ILogger<IndexModel> logger)
    {
        _smsService = smsService;
        _context = context;
        _gsmGateway = gsmGateway;
        _semaphoreGateway = semaphoreGateway;
        _logger = logger;
    }

    public SmsStatistics Stats { get; set; } = new();
    public GatewayHealthStatus GsmHealth { get; set; } = new();
    public List<SmsLog> RecentLogs { get; set; } = new();

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
