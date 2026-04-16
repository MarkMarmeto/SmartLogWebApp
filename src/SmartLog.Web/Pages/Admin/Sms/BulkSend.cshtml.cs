using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class BulkSendModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ISmsService _smsService;
    private readonly ILogger<BulkSendModel> _logger;

    public BulkSendModel(
        ApplicationDbContext context,
        ISmsService smsService,
        ILogger<BulkSendModel> logger)
    {
        _context = context;
        _smsService = smsService;
        _logger = logger;
    }

    [BindProperty]
    public string Message { get; set; } = string.Empty;

    [BindProperty]
    public List<string> SelectedGrades { get; set; } = new();

    [BindProperty]
    public List<string> SelectedPrograms { get; set; } = new();

    [BindProperty]
    public string? SelectedSection { get; set; }

    [BindProperty]
    public bool ActiveOnly { get; set; } = true;

    /// <summary>
    /// US0055: Per-broadcast gateway override. "GSM_MODEM", "SEMAPHORE", or null = system default.
    /// </summary>
    [BindProperty]
    public string? PreferredProvider { get; set; }

    public List<string> AvailableGrades { get; set; } = new();
    public List<Data.Entities.Program> AvailablePrograms { get; set; } = new();
    public List<string> AvailableSections { get; set; } = new();
    public int TotalActiveStudents { get; set; }
    public Dictionary<string, int> StudentCountByGrade { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadFormDataAsync();
    }

    public async Task<IActionResult> OnGetRecipientCountAsync(
        [FromQuery] List<string>? grades,
        [FromQuery] List<string>? programs,
        [FromQuery] string? section,
        [FromQuery] bool activeOnly = true)
    {
        var query = _context.Students.Where(s => s.SmsEnabled);

        if (activeOnly)
            query = query.Where(s => s.IsActive);

        if (grades != null && grades.Any())
            query = query.Where(s => grades.Contains(s.GradeLevel));

        if (programs != null && programs.Any())
            query = query.Where(s => s.Program != null && programs.Contains(s.Program));

        if (!string.IsNullOrWhiteSpace(section))
            query = query.Where(s => s.Section == section);

        var count = await query.CountAsync();
        return new JsonResult(new { count });
    }

    public async Task<IActionResult> OnPostAsync()
    {
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

        try
        {
            var query = _context.Students.Where(s => s.SmsEnabled);

            if (ActiveOnly)
            {
                query = query.Where(s => s.IsActive);
            }

            if (SelectedGrades.Any())
                query = query.Where(s => SelectedGrades.Contains(s.GradeLevel));

            if (SelectedPrograms.Any())
                query = query.Where(s => s.Program != null && SelectedPrograms.Contains(s.Program));

            if (!string.IsNullOrWhiteSpace(SelectedSection))
                query = query.Where(s => s.Section == SelectedSection);

            var students = await query
                .Select(s => s.ParentPhone)
                .Distinct()
                .ToListAsync();

            var provider = string.IsNullOrEmpty(PreferredProvider) ? null : PreferredProvider;
            int queuedCount = 0;
            foreach (var phone in students)
            {
                if (!await _smsService.IsDuplicateAsync(phone, Message, 5))
                {
                    await _smsService.QueueCustomSmsAsync(phone, Message, SmsPriority.Normal, "CUSTOM", null, provider);
                    queuedCount++;
                }
            }

            StatusMessage = $"Queued {queuedCount} messages for delivery.";
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
        AvailableGrades = await _context.GradeLevels
            .OrderBy(g => g.SortOrder)
            .Select(g => g.Code)
            .ToListAsync();

        AvailablePrograms = await _context.Programs
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Code)
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

        StudentCountByGrade = await _context.Students
            .Where(s => s.IsActive && s.SmsEnabled)
            .GroupBy(s => s.GradeLevel)
            .Select(g => new { Grade = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Grade, x => x.Count);
    }
}
