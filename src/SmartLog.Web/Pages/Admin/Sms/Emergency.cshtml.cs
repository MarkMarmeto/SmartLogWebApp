using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class EmergencyModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ISmsService _smsService;
    private readonly ILogger<EmergencyModel> _logger;

    public EmergencyModel(
        ApplicationDbContext context,
        ISmsService smsService,
        ILogger<EmergencyModel> logger)
    {
        _context = context;
        _smsService = smsService;
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

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        // Get all grade levels
        AvailableGrades = await _context.GradeLevels
            .OrderBy(g => g.SortOrder)
            .Select(g => g.Code)
            .ToListAsync();

        // Get student counts
        TotalActiveStudents = await _context.Students
            .Where(s => s.IsActive && s.SmsEnabled)
            .CountAsync();

        StudentCountByGrade = await _context.Students
            .Where(s => s.IsActive && s.SmsEnabled)
            .GroupBy(s => s.GradeLevel)
            .Select(g => new { Grade = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Grade, x => x.Count);
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

        try
        {
            // Queue emergency announcements
            await _smsService.QueueEmergencyAnnouncementAsync(
                Message,
                Language,
                AffectedGrades.Any() ? AffectedGrades : null);

            var gradeText = AffectedGrades.Any()
                ? $"grades {string.Join(", ", AffectedGrades)}"
                : "all grades";

            StatusMessage = $"Emergency broadcast queued successfully for {gradeText}.";
            _logger.LogWarning("Emergency broadcast sent: {Message} to {Grades}",
                Message, gradeText);

            return RedirectToPage("/Admin/Sms/Index");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to queue emergency broadcast.";
            _logger.LogError(ex, "Error queuing emergency broadcast");
            return RedirectToPage();
        }
    }
}
