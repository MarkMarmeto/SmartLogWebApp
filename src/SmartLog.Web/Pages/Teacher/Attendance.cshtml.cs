using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Teacher;

[Authorize(Roles = "Teacher")]
public class AttendanceModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAttendanceService _attendanceService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AttendanceModel(
        ApplicationDbContext context,
        IAttendanceService attendanceService,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _attendanceService = attendanceService;
        _userManager = userManager;
    }

    public Faculty? Faculty { get; set; }
    public Section? Section { get; set; }
    public AttendanceSummary Summary { get; set; } = new();
    public List<StudentAttendanceRecord> Students { get; set; } = new();
    public DateTime SelectedDate { get; set; }
    public string? SearchTerm { get; set; }
    public string? StatusFilter { get; set; }
    public int TotalCount { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    public async Task<IActionResult> OnGetAsync(
        DateTime? selectedDate,
        string? search,
        string? status,
        int page = 1)
    {
        SelectedDate = selectedDate?.Date ?? DateTime.Today;
        SearchTerm = search;
        StatusFilter = status;
        PageNumber = Math.Max(1, page);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        Faculty = await _context.Faculties
            .FirstOrDefaultAsync(f => f.UserId == user.Id);

        if (Faculty == null) return Page();

        Section = await _context.Sections
            .Include(s => s.GradeLevel)
            .FirstOrDefaultAsync(s => s.AdviserId == Faculty.Id && s.IsActive);

        if (Section == null) return Page();

        Summary = await _attendanceService.GetAttendanceSummaryAsync(
            SelectedDate,
            Section.GradeLevel.Code,
            Section.Name);

        TotalCount = await _attendanceService.GetAttendanceCountAsync(
            SelectedDate,
            Section.GradeLevel.Code,
            Section.Name,
            SearchTerm,
            StatusFilter);

        Students = await _attendanceService.GetAttendanceListAsync(
            SelectedDate,
            Section.GradeLevel.Code,
            Section.Name,
            SearchTerm,
            StatusFilter,
            PageNumber,
            PageSize);

        return Page();
    }
}
