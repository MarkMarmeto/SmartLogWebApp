using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class ScheduledSendModel : PageModel
{
    private readonly ISmsService _smsService;
    private readonly ILogger<ScheduledSendModel> _logger;

    public ScheduledSendModel(
        ISmsService smsService,
        ILogger<ScheduledSendModel> logger)
    {
        _smsService = smsService;
        _logger = logger;
    }

    [BindProperty]
    public string PhoneNumber { get; set; } = string.Empty;

    [BindProperty]
    public string Message { get; set; } = string.Empty;

    [BindProperty]
    public DateTime? ScheduledAt { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber))
        {
            ErrorMessage = "Phone number is required.";
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            ErrorMessage = "Message is required.";
            return RedirectToPage();
        }

        if (Message.Length > 320)
        {
            ErrorMessage = "Message must be 320 characters or less.";
            return RedirectToPage();
        }

        if (ScheduledAt.HasValue && ScheduledAt.Value.ToUniversalTime() <= DateTime.UtcNow)
        {
            ErrorMessage = "Scheduled time must be in the future.";
            return RedirectToPage();
        }

        try
        {
            var scheduledUtc = ScheduledAt?.ToUniversalTime();

            await _smsService.QueueCustomSmsAsync(
                PhoneNumber,
                Message,
                SmsPriority.Normal,
                "CUSTOM",
                scheduledUtc);

            var timeText = scheduledUtc.HasValue
                ? $"scheduled for {ScheduledAt!.Value:MMM dd, yyyy h:mm tt}"
                : "queued for immediate delivery";

            StatusMessage = $"SMS {timeText} to {PhoneNumber}.";
            _logger.LogInformation("Scheduled SMS to {Phone} at {Time}", PhoneNumber, scheduledUtc);

            return RedirectToPage("/Admin/Sms/Queue");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to schedule SMS.";
            _logger.LogError(ex, "Error scheduling SMS to {Phone}", PhoneNumber);
            return RedirectToPage();
        }
    }
}
