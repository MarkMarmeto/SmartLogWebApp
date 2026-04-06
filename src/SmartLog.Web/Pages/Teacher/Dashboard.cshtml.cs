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
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAttendanceService _attendanceService;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardModel(
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
    public DateTime DisplayDate { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        DisplayDate = DateTime.Today;

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
            DisplayDate,
            Section.GradeLevel.Code,
            Section.Name);

        Students = await _attendanceService.GetAttendanceListAsync(
            DisplayDate,
            Section.GradeLevel.Code,
            Section.Name,
            pageSize: 200);

        return Page();
    }
}
