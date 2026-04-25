using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Models.Sms;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class EmergencyModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ISmsService _smsService;
    private readonly ISmsTemplateService _templateService;
    private readonly ISmsSettingsService _smsSettingsService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<EmergencyModel> _logger;

    public EmergencyModel(
        ApplicationDbContext context,
        ISmsService smsService,
        ISmsTemplateService templateService,
        ISmsSettingsService smsSettingsService,
        UserManager<ApplicationUser> userManager,
        ILogger<EmergencyModel> logger)
    {
        _context = context;
        _smsService = smsService;
        _templateService = templateService;
        _smsSettingsService = smsSettingsService;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public BroadcastMessageBodies MessageBodies { get; set; } = new();

    [BindProperty]
    public string? TargetingJson { get; set; }

    public bool IsSmsEnabled { get; set; }
    public List<ProgramWithGrades> ProgramsWithGrades { get; set; } = new();
    public int TotalActiveStudents { get; set; }
    public string TemplatePrefixEn { get; set; } = "[ALERT]";
    public string TemplatePrefixFil { get; set; } = "[ALERTO]";
    public string TemplateSuffixEn { get; set; } = string.Empty;
    public string TemplateSuffixFil { get; set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

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

    public async Task OnGetAsync()
    {
        IsSmsEnabled = await _smsSettingsService.IsSmsEnabledAsync();

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

        var template = await _templateService.GetTemplateByCodeAsync("EMERGENCY");
        if (template != null)
        {
            var enIdx = template.TemplateEn.IndexOf("{Message}");
            var filIdx = template.TemplateFil.IndexOf("{Message}");
            if (enIdx >= 0)
            {
                TemplatePrefixEn = template.TemplateEn[..enIdx].Trim();
                TemplateSuffixEn = template.TemplateEn[(enIdx + 9)..].Trim();
            }
            if (filIdx >= 0)
            {
                TemplatePrefixFil = template.TemplateFil[..filIdx].Trim();
                TemplateSuffixFil = template.TemplateFil[(filIdx + 9)..].Trim();
            }
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
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

        if (!string.IsNullOrWhiteSpace(MessageBodies.EnglishBody) && MessageBodies.EnglishBody.Length > 200)
        {
            ErrorMessage = "English message must be 200 characters or less.";
            return RedirectToPage();
        }
        if (!string.IsNullOrWhiteSpace(MessageBodies.FilipinoBody) && MessageBodies.FilipinoBody.Length > 200)
        {
            ErrorMessage = "Filipino message must be 200 characters or less.";
            return RedirectToPage();
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

            var (_, skipped) = await _smsService.QueueEmergencyAnnouncementAsync(
                MessageBodies,
                historyGrades.Any() ? historyGrades : null,
                historyPrograms.Any() ? historyPrograms : null,
                _userManager.GetUserId(User),
                createdByName,
                provider,
                preResolvedIds);

            var programText = historyPrograms.Any() ? string.Join(", ", historyPrograms) : "all programs";
            var gradeText = historyGrades.Any() ? $"grades {string.Join(", ", historyGrades)}" : "all grades";

            StatusMessage = skipped > 0
                ? $"Emergency broadcast queued for {gradeText} — {programText}. {skipped} student(s) skipped — language preference does not match selected mode."
                : $"Emergency broadcast queued successfully for {gradeText} — {programText}.";

            _logger.LogWarning("Emergency broadcast sent by {User} to {Grades}", User.Identity?.Name, gradeText);

            return RedirectToPage("/Admin/Sms/Broadcasts");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to queue emergency broadcast.";
            _logger.LogError(ex, "Error queuing emergency broadcast");
            return RedirectToPage();
        }
    }
}
