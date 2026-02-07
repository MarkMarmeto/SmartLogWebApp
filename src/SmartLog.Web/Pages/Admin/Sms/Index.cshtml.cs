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

    public IndexModel(
        ISmsService smsService,
        ApplicationDbContext context,
        GsmModemGateway gsmGateway,
        SemaphoreGateway semaphoreGateway)
    {
        _smsService = smsService;
        _context = context;
        _gsmGateway = gsmGateway;
        _semaphoreGateway = semaphoreGateway;
    }

    public SmsStatistics Stats { get; set; } = new();
    public GatewayHealthStatus GsmHealth { get; set; } = new();
    public GatewayHealthStatus SemaphoreHealth { get; set; } = new();
    public List<SmsLog> RecentLogs { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        // Get statistics for last 7 days
        var startDate = DateTime.UtcNow.AddDays(-7);
        Stats = await _smsService.GetStatisticsAsync(startDate);

        // Get gateway health status
        GsmHealth = await _gsmGateway.GetHealthStatusAsync();
        SemaphoreHealth = await _semaphoreGateway.GetHealthStatusAsync();

        // Get recent logs (last 10)
        RecentLogs = await _context.SmsLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(10)
            .ToListAsync();
    }
}
