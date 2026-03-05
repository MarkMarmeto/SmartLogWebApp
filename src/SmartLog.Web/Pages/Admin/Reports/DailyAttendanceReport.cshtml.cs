using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin.Reports;

/// <summary>
/// Daily Attendance Report page.
/// Implements US0045 (Daily Attendance Report).
/// </summary>
[Authorize(Policy = "CanViewStudents")]
public class DailyAttendanceReportModel : PageModel
{
    private readonly IAttendanceService _attendanceService;
    private readonly ApplicationDbContext _context;

    public DailyAttendanceReportModel(IAttendanceService attendanceService, ApplicationDbContext context)
    {
        _attendanceService = attendanceService;
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? SelectedDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? GradeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SectionFilter { get; set; }

    public DateTime ReportDate => SelectedDate ?? DateTime.Today;
    public AttendanceSummary Summary { get; set; } = new();
    public List<StudentAttendanceRecord> PresentStudents { get; set; } = new();
    public List<StudentAttendanceRecord> AbsentStudents { get; set; } = new();
    public List<StudentAttendanceRecord> DepartedStudents { get; set; } = new();
    public List<GradeLevel> GradeLevels { get; set; } = new();

    public async Task OnGetAsync()
    {
        GradeLevels = _context.GradeLevels
            .Where(g => g.IsActive)
            .OrderBy(g => g.SortOrder)
            .ToList();

        var targetDate = ReportDate;

        // US0045-AC1: Load summary statistics
        Summary = await _attendanceService.GetAttendanceSummaryAsync(
            targetDate, GradeFilter, SectionFilter);

        // US0045-AC2: Load all attendance records grouped by status
        var allRecords = await _attendanceService.GetAttendanceListAsync(
            targetDate, GradeFilter, SectionFilter, null, null, 1, 10000);

        // Group by status for organized display
        PresentStudents = allRecords.Where(r => r.Status == "Present").ToList();
        AbsentStudents = allRecords.Where(r => r.Status == "Absent").ToList();
        DepartedStudents = allRecords.Where(r => r.Status == "Departed").ToList();
    }
}
