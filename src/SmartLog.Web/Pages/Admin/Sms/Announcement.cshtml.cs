using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Models.Sms;
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
    private readonly ISmsSettingsService _smsSettingsService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AnnouncementModel> _logger;

    private static readonly TimeZoneInfo PhilippineTime =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

    public AnnouncementModel(
        ApplicationDbContext context,
        ISmsService smsService,
        ISmsTemplateService templateService,
        IAppSettingsService appSettingsService,
        ISmsSettingsService smsSettingsService,
        UserManager<ApplicationUser> userManager,
        ILogger<AnnouncementModel> logger)
    {
        _context = context;
        _smsService = smsService;
        _templateService = templateService;
        _appSettingsService = appSettingsService;
        _smsSettingsService = smsSettingsService;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public BroadcastMessageBodies MessageBodies { get; set; } = new();

    [BindProperty]
    public string? TargetingJson { get; set; }

    [BindProperty]
    public string? ScheduledAtLocal { get; set; }

    public bool IsSmsEnabled { get; set; }
    public List<ProgramWithGrades> ProgramsWithGrades { get; set; } = new();
    public int TotalActiveStudents { get; set; }
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
        IsSmsEnabled = await _smsSettingsService.IsSmsEnabledAsync();
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnGetRecipientCountAsync([FromQuery] string? targetingJson)
    {
        if (!string.IsNullOrWhiteSpace(targetingJson))
        {
            var filters = JsonSerializer.Deserialize<List<ProgramGradeFilter>>(targetingJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            var ids = await _smsService.ResolveStudentIdsByFiltersAsync(filters);
            return new JsonResult(new { count = ids.Count });
        }
        var count = await _context.Students.CountAsync(s => s.IsActive && s.SmsEnabled);
        return new JsonResult(new { count });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Validate message bodies based on selected mode
        if (MessageBodies.Mode != BroadcastLanguageMode.FilipinoOnly &&
            string.IsNullOrWhiteSpace(MessageBodies.EnglishBody))
        {
            ErrorMessage = "English message is required.";
            return RedirectToPage();
        }
        if (MessageBodies.Mode == BroadcastLanguageMode.Both &&
            string.IsNullOrWhiteSpace(MessageBodies.FilipinoBody))
        {
            ErrorMessage = "Filipino message is required when 'Both' is selected.";
            return RedirectToPage();
        }
        if (MessageBodies.Mode == BroadcastLanguageMode.FilipinoOnly &&
            string.IsNullOrWhiteSpace(MessageBodies.FilipinoBody))
        {
            ErrorMessage = "Filipino message is required.";
            return RedirectToPage();
        }

        if (!string.IsNullOrWhiteSpace(MessageBodies.EnglishBody) && MessageBodies.EnglishBody.Length > 160)
        {
            ErrorMessage = "English message must be 160 characters or less.";
            return RedirectToPage();
        }
        if (!string.IsNullOrWhiteSpace(MessageBodies.FilipinoBody) && MessageBodies.FilipinoBody.Length > 160)
        {
            ErrorMessage = "Filipino message must be 160 characters or less.";
            return RedirectToPage();
        }

        // Parse optional schedule
        DateTime? scheduledAtUtc = null;
        if (!string.IsNullOrWhiteSpace(ScheduledAtLocal))
        {
            if (DateTime.TryParse(ScheduledAtLocal, out var localParsed))
            {
                var utc = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(localParsed, DateTimeKind.Unspecified), PhilippineTime);
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

        List<ProgramGradeFilter> filters = new();
        if (!string.IsNullOrWhiteSpace(TargetingJson))
        {
            filters = JsonSerializer.Deserialize<List<ProgramGradeFilter>>(TargetingJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }

        var preResolvedIds = filters.Any()
            ? await _smsService.ResolveStudentIdsByFiltersAsync(filters)
            : null;

        var recipientCount = preResolvedIds?.Count
            ?? await _context.Students.CountAsync(s => s.IsActive && s.SmsEnabled);

        if (recipientCount == 0)
        {
            ErrorMessage = "No students with SMS enabled match the selected targeting. Adjust the filter or check that students have SMS enabled.";
            return RedirectToPage();
        }

        try
        {
            var user = await _userManager.GetUserAsync(User);
            var createdByName = user != null
                ? $"{user.FirstName} {user.LastName}".Trim()
                : User.Identity?.Name;

            var provider = await _smsSettingsService.GetSettingAsync("Sms.DefaultProvider");

            var historyGrades = filters.SelectMany(f => f.GradeLevelCodes).Distinct().ToList();
            var historyPrograms = filters.Select(f => f.ProgramCode).Distinct().ToList();

            var (broadcastId, skipped) = await _smsService.QueueAnnouncementAsync(
                MessageBodies,
                historyGrades.Any() ? historyGrades : null,
                historyPrograms.Any() ? historyPrograms : null,
                scheduledAtUtc,
                _userManager.GetUserId(User),
                createdByName,
                provider,
                preResolvedIds);

            var programText = historyPrograms.Any() ? string.Join(", ", historyPrograms) : "all programs";
            var gradeText = historyGrades.Any() ? $"grades {string.Join(", ", historyGrades)}" : "all grades";
            var targetSummary = $"{programText} — {gradeText}";

            if (scheduledAtUtc.HasValue)
            {
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(scheduledAtUtc.Value, PhilippineTime);
                StatusMessage = $"Announcement scheduled for {localTime:MMM d, yyyy h:mm tt} (PHT) to {targetSummary}.";
            }
            else
            {
                StatusMessage = skipped > 0
                    ? $"Announcement queued to {targetSummary}. {skipped} student(s) skipped — language preference does not match selected mode."
                    : $"Announcement queued successfully to {targetSummary}.";
            }

            _logger.LogInformation("Announcement broadcast created by {User} to {Target}", User.Identity?.Name, targetSummary);

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
        ProgramsWithGrades = await _context.Programs
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Code)
            .Select(p => new ProgramWithGrades
            {
                Code = p.Code,
                Name = p.Name,
                Grades = p.GradeLevelPrograms
                    .OrderBy(glp => glp.GradeLevel.SortOrder)
                    .Select(glp => new GradeLevelItem
                    {
                        Code = glp.GradeLevel.Code,
                        Name = glp.GradeLevel.Name
                    })
                    .ToList()
            })
            .ToListAsync();

        TotalActiveStudents = await _context.Students
            .Where(s => s.IsActive && s.SmsEnabled)
            .CountAsync();

        var schoolName = await _appSettingsService.GetAsync("System.SchoolName") ?? "School";
        var schoolPhone = await _appSettingsService.GetAsync("System.SchoolPhone") ?? "";

        var template = await _templateService.GetTemplateByCodeAsync("ANNOUNCEMENT");
        if (template != null)
        {
            var enResolved = template.TemplateEn
                .Replace("{SchoolName}", schoolName).Replace("{SchoolPhone}", schoolPhone);
            var filResolved = template.TemplateFil
                .Replace("{SchoolName}", schoolName).Replace("{SchoolPhone}", schoolPhone);

            var enIdx = enResolved.IndexOf("{Message}");
            var filIdx = filResolved.IndexOf("{Message}");

            TemplatePrefixEn = enIdx >= 0 ? enResolved[..enIdx] : enResolved;
            TemplateSuffixEn = enIdx >= 0 ? enResolved[(enIdx + 9)..] : string.Empty;
            TemplatePrefixFil = filIdx >= 0 ? filResolved[..filIdx] : filResolved;
            TemplateSuffixFil = filIdx >= 0 ? filResolved[(filIdx + 9)..] : string.Empty;
        }
    }
}
