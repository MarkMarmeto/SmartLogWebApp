using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class AnnouncementModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ISmsService _smsService;
    private readonly ISmsTemplateService _templateService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AnnouncementModel> _logger;

    // Philippine Standard Time (UTC+8)
    private static readonly TimeZoneInfo PhilippineTime =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

    public AnnouncementModel(
        ApplicationDbContext context,
        ISmsService smsService,
        ISmsTemplateService templateService,
        IAppSettingsService appSettingsService,
        UserManager<ApplicationUser> userManager,
        ILogger<AnnouncementModel> logger)
    {
        _context = context;
        _smsService = smsService;
        _templateService = templateService;
        _appSettingsService = appSettingsService;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public string Message { get; set; } = string.Empty;

    [BindProperty]
    public string? Language { get; set; }

    [BindProperty]
    public List<string> AffectedGrades { get; set; } = new();

    /// <summary>
    /// Scheduled time in Philippine local time (datetime-local input), empty = send immediately
    /// </summary>
    [BindProperty]
    public string? ScheduledAtLocal { get; set; }

    public List<string> AvailableGrades { get; set; } = new();
    public int TotalActiveStudents { get; set; }
    public Dictionary<string, int> StudentCountByGrade { get; set; } = new();
    public string TemplatePrefixEn { get; set; } = string.Empty;
    public string TemplatePrefixFil { get; set; } = string.Empty;
    public string TemplateSuffixEn { get; set; } = string.Empty;
    public string TemplateSuffixFil { get; set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Message))
        {
            ErrorMessage = "Message is required.";
            return RedirectToPage();
        }

        if (Message.Length > 160)
        {
            ErrorMessage = "Message must be 160 characters or less.";
            return RedirectToPage();
        }

        // Parse optional schedule — input is in PH local time
        DateTime? scheduledAtUtc = null;
        if (!string.IsNullOrWhiteSpace(ScheduledAtLocal))
        {
            if (DateTime.TryParse(ScheduledAtLocal, out var localParsed))
            {
                var utc = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(localParsed, DateTimeKind.Unspecified),
                    PhilippineTime);

                if (utc <= DateTime.UtcNow.AddMinutes(2))
                {
                    ErrorMessage = "Scheduled time must be at least a few minutes in the future.";
                    return RedirectToPage();
                }
                scheduledAtUtc = utc;
            }
            else
            {
                ErrorMessage = "Invalid scheduled time.";
                return RedirectToPage();
            }
        }

        try
        {
            var user = await _userManager.GetUserAsync(User);
            var createdByName = user != null
                ? $"{user.FirstName} {user.LastName}".Trim()
                : User.Identity?.Name;

            var broadcastId = await _smsService.QueueAnnouncementAsync(
                Message,
                Language,
                AffectedGrades.Any() ? AffectedGrades : null,
                scheduledAtUtc,
                _userManager.GetUserId(User),
                createdByName);

            var gradeText = AffectedGrades.Any()
                ? $"grades {string.Join(", ", AffectedGrades)}"
                : "all grades";

            if (scheduledAtUtc.HasValue)
            {
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(scheduledAtUtc.Value, PhilippineTime);
                StatusMessage = $"Announcement scheduled for {localTime:MMM d, yyyy h:mm tt} (PHT) for {gradeText}.";
            }
            else
            {
                StatusMessage = $"Announcement queued successfully for {gradeText}.";
            }

            _logger.LogInformation("Announcement broadcast created by {User}: {Message} to {Grades}",
                User.Identity?.Name, Message, gradeText);

            return RedirectToPage("/Admin/Sms/Broadcasts");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to queue announcement.";
            _logger.LogError(ex, "Error queuing announcement broadcast");
            return RedirectToPage();
        }
    }

    private async Task LoadPageDataAsync()
    {
        AvailableGrades = await _context.GradeLevels
            .OrderBy(g => g.SortOrder)
            .Select(g => g.Code)
            .ToListAsync();

        TotalActiveStudents = await _context.Students
            .Where(s => s.IsActive && s.SmsEnabled)
            .CountAsync();

        StudentCountByGrade = await _context.Students
            .Where(s => s.IsActive && s.SmsEnabled)
            .GroupBy(s => s.GradeLevel)
            .Select(g => new { Grade = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Grade, x => x.Count);

        var schoolName = await _appSettingsService.GetAsync("System.SchoolName") ?? "School";
        var schoolPhone = await _appSettingsService.GetAsync("System.SchoolPhone") ?? "";

        var template = await _templateService.GetTemplateByCodeAsync("ANNOUNCEMENT");
        if (template != null)
        {
            var enResolved = template.TemplateEn
                .Replace("{SchoolName}", schoolName)
                .Replace("{SchoolPhone}", schoolPhone);
            var filResolved = template.TemplateFil
                .Replace("{SchoolName}", schoolName)
                .Replace("{SchoolPhone}", schoolPhone);

            var enIdx = enResolved.IndexOf("{Message}");
            var filIdx = filResolved.IndexOf("{Message}");

            TemplatePrefixEn = enIdx >= 0 ? enResolved[..enIdx] : enResolved;
            TemplateSuffixEn = enIdx >= 0 ? enResolved[(enIdx + 9)..] : string.Empty;
            TemplatePrefixFil = filIdx >= 0 ? filResolved[..filIdx] : filResolved;
            TemplateSuffixFil = filIdx >= 0 ? filResolved[(filIdx + 9)..] : string.Empty;
        }
    }
}
