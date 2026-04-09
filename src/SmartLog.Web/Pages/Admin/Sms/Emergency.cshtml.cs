using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class EmergencyModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ISmsService _smsService;
    private readonly ISmsTemplateService _templateService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<EmergencyModel> _logger;

    public EmergencyModel(
        ApplicationDbContext context,
        ISmsService smsService,
        ISmsTemplateService templateService,
        UserManager<ApplicationUser> userManager,
        ILogger<EmergencyModel> logger)
    {
        _context = context;
        _smsService = smsService;
        _templateService = templateService;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public string Message { get; set; } = string.Empty;

    [BindProperty]
    public string? Language { get; set; }

    [BindProperty]
    public List<string> AffectedGrades { get; set; } = new();

    public List<string> AvailableGrades { get; set; } = new();
    public int TotalActiveStudents { get; set; }
    public Dictionary<string, int> StudentCountByGrade { get; set; } = new();
    public string TemplatePrefixEn { get; set; } = "[ALERT]";
    public string TemplatePrefixFil { get; set; } = "[ALERTO]";

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
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

        // Load actual template prefix for accurate preview
        var template = await _templateService.GetTemplateByCodeAsync("EMERGENCY");
        if (template != null)
        {
            // Extract the prefix (everything before {Message})
            var enIdx = template.TemplateEn.IndexOf("{Message}");
            var filIdx = template.TemplateFil.IndexOf("{Message}");
            if (enIdx >= 0) TemplatePrefixEn = template.TemplateEn[..enIdx].Trim();
            if (filIdx >= 0) TemplatePrefixFil = template.TemplateFil[..filIdx].Trim();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Message))
        {
            ErrorMessage = "Message is required.";
            return RedirectToPage();
        }

        if (Message.Length > 200)
        {
            ErrorMessage = "Message must be 200 characters or less.";
            return RedirectToPage();
        }

        // Server-side guard: count recipients before sending
        var recipientQuery = _context.Students.Where(s => s.IsActive && s.SmsEnabled);
        if (AffectedGrades.Any())
            recipientQuery = recipientQuery.Where(s => AffectedGrades.Contains(s.GradeLevel));
        var recipientCount = await recipientQuery.CountAsync();

        if (recipientCount == 0)
        {
            ErrorMessage = AffectedGrades.Any()
                ? $"No students with SMS enabled found in grades {string.Join(", ", AffectedGrades)}. Adjust the grade filter or check that students have SMS enabled."
                : "No active students with SMS enabled were found. Enable SMS for students before sending a broadcast.";
            return RedirectToPage();
        }

        try
        {
            var user = await _userManager.GetUserAsync(User);
            var createdByName = user != null
                ? $"{user.FirstName} {user.LastName}".Trim()
                : User.Identity?.Name;

            // Queue emergency announcements
            await _smsService.QueueEmergencyAnnouncementAsync(
                Message,
                Language,
                AffectedGrades.Any() ? AffectedGrades : null,
                _userManager.GetUserId(User),
                createdByName);

            var gradeText = AffectedGrades.Any()
                ? $"grades {string.Join(", ", AffectedGrades)}"
                : "all grades";

            StatusMessage = $"Emergency broadcast queued successfully for {gradeText}.";
            _logger.LogWarning("Emergency broadcast sent by {User}: {Message} to {Grades}",
                User.Identity?.Name, Message, gradeText);

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
