using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class BroadcastsModel : PageModel
{
    private readonly ISmsService _smsService;
    private readonly ILogger<BroadcastsModel> _logger;

    // Philippine Standard Time (UTC+8)
    private static readonly TimeZoneInfo PhilippineTime =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

    public BroadcastsModel(ISmsService smsService, ILogger<BroadcastsModel> logger)
    {
        _smsService = smsService;
        _logger = logger;
    }

    public List<Broadcast> Broadcasts { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        Broadcasts = await _smsService.GetBroadcastsAsync(pageSize: 50);
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id)
    {
        var cancelled = await _smsService.CancelBroadcastAsync(id);

        if (cancelled >= 0)
        {
            StatusMessage = $"Broadcast cancelled. {cancelled} pending message(s) removed from queue.";
            _logger.LogInformation("Broadcast {Id} cancelled by {User} — {Count} messages cancelled",
                id, User.Identity?.Name, cancelled);
        }
        else
        {
            ErrorMessage = "Failed to cancel broadcast.";
        }

        return RedirectToPage();
    }

    /// <summary>Convert UTC time to PH local time for display</summary>
    public string ToPhilippineTime(DateTime? utcTime)
    {
        if (!utcTime.HasValue) return "-";
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcTime.Value, DateTimeKind.Utc),
            PhilippineTime);
        return local.ToString("MMM d, yyyy h:mm tt");
    }
}
