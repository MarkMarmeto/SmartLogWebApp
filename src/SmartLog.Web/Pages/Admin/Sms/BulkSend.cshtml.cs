using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Models.Sms;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class BulkSendModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ISmsService _smsService;
    private readonly ISmsSettingsService _smsSettingsService;
    private readonly ILogger<BulkSendModel> _logger;

    public BulkSendModel(
        ApplicationDbContext context,
        ISmsService smsService,
        ISmsSettingsService smsSettingsService,
        ILogger<BulkSendModel> logger)
    {
        _context = context;
        _smsService = smsService;
        _smsSettingsService = smsSettingsService;
        _logger = logger;
    }

    [BindProperty]
    public BroadcastMessageBodies MessageBodies { get; set; } = new();

    [BindProperty]
    public string? TargetingJson { get; set; }

    [BindProperty]
    public string? SelectedSection { get; set; }

    [BindProperty]
    public bool ActiveOnly { get; set; } = true;

    public bool IsSmsEnabled { get; set; }
    public BroadcastTargetingViewModel Targeting { get; set; } = new();
    public List<string> AvailableSections { get; set; } = new();
    public int TotalActiveStudents { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        IsSmsEnabled = await _smsSettingsService.IsSmsEnabledAsync();
        await LoadFormDataAsync();
    }

    public async Task<IActionResult> OnGetRecipientCountAsync(
        [FromQuery] string? targetingJson,
        [FromQuery] string? section,
        [FromQuery] bool activeOnly = true)
    {
        List<Guid>? resolvedIds = null;
        if (!string.IsNullOrWhiteSpace(targetingJson))
        {
            var filters = JsonSerializer.Deserialize<List<ProgramGradeFilter>>(targetingJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            if (filters.Any())
                resolvedIds = await _smsService.ResolveStudentIdsByFiltersAsync(filters, activeOnly);
        }

        var query = _context.Students.Where(s => s.SmsEnabled);
        if (activeOnly) query = query.Where(s => s.IsActive);
        if (resolvedIds != null) query = query.Where(s => resolvedIds.Contains(s.Id));
        if (!string.IsNullOrWhiteSpace(section)) query = query.Where(s => s.Section == section);

        var count = await query.CountAsync();
        return new JsonResult(new { count });
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

        if (!string.IsNullOrWhiteSpace(MessageBodies.EnglishBody) && MessageBodies.EnglishBody.Length > 320)
        {
            ErrorMessage = "English message must be 320 characters or less.";
            return RedirectToPage();
        }
        if (!string.IsNullOrWhiteSpace(MessageBodies.FilipinoBody) && MessageBodies.FilipinoBody.Length > 320)
        {
            ErrorMessage = "Filipino message must be 320 characters or less.";
            return RedirectToPage();
        }

        try
        {
            List<Guid>? resolvedIds = null;
            if (!string.IsNullOrWhiteSpace(TargetingJson))
            {
                var filters = JsonSerializer.Deserialize<List<ProgramGradeFilter>>(TargetingJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                if (filters.Any())
                    resolvedIds = await _smsService.ResolveStudentIdsByFiltersAsync(filters, ActiveOnly);
            }

            var query = _context.Students.Where(s => s.SmsEnabled);
            if (ActiveOnly) query = query.Where(s => s.IsActive);
            if (resolvedIds != null) query = query.Where(s => resolvedIds.Contains(s.Id));
            if (!string.IsNullOrWhiteSpace(SelectedSection)) query = query.Where(s => s.Section == SelectedSection);

            var students = await query.Select(s => new { s.ParentPhone, s.SmsLanguage }).ToListAsync();

            var provider = await _smsSettingsService.GetSettingAsync("Sms.DefaultProvider");
            int queuedCount = 0;
            int skippedCount = 0;

            // Track phones already queued to avoid per-number duplicates
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var student in students)
            {
                if (!MessageBodies.ShouldSendToStudent(student.SmsLanguage))
                {
                    skippedCount++;
                    continue;
                }

                var body = MessageBodies.GetBodyForLanguage(student.SmsLanguage);
                if (string.IsNullOrWhiteSpace(student.ParentPhone)) continue;
                if (!seen.Add(student.ParentPhone)) continue;

                if (!await _smsService.IsDuplicateAsync(student.ParentPhone, body, 5))
                {
                    await _smsService.QueueCustomSmsAsync(student.ParentPhone, body, SmsPriority.Normal, "CUSTOM", null, provider);
                    queuedCount++;
                }
            }

            StatusMessage = skippedCount > 0
                ? $"Queued {queuedCount} messages. {skippedCount} student(s) skipped — language preference does not match selected mode."
                : $"Queued {queuedCount} messages for delivery.";
            _logger.LogInformation("Bulk SMS sent: {Count} messages queued", queuedCount);

            return RedirectToPage("/Admin/Sms/Queue");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to queue bulk SMS.";
            _logger.LogError(ex, "Error queuing bulk SMS");
            return RedirectToPage();
        }
    }

    private async Task LoadFormDataAsync()
    {
        Targeting.ProgramsWithGrades = await _context.Programs
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

        Targeting.NonGradedSections = await _context.Sections
            .Include(s => s.GradeLevel)
            .Where(s => s.IsActive && s.GradeLevel.Code == "NG")
            .OrderBy(s => s.Name)
            .Select(s => new NonGradedSectionItem { Name = s.Name })
            .ToListAsync();

        AvailableSections = await _context.Students
            .Where(s => s.IsActive)
            .Select(s => s.Section)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();

        TotalActiveStudents = await _context.Students
            .Where(s => s.IsActive && s.SmsEnabled)
            .CountAsync();
    }
}
