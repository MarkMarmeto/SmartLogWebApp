using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Attendance dashboard page.
/// Implements US0034 (School-Wide Attendance Dashboard), US0036 (Filtering and Search), US0037 (Auto-Refresh).
/// </summary>
[Authorize(Policy = "CanViewStudents")]
public class AttendanceDashboardModel : PageModel
{
    private readonly IAttendanceService _attendanceService;
    private readonly ApplicationDbContext _context;
    private readonly IAcademicYearService _academicYearService;
    private readonly ITimezoneService _timezoneService;

    public AttendanceDashboardModel(
        IAttendanceService attendanceService,
        ApplicationDbContext context,
        IAcademicYearService academicYearService,
        ITimezoneService timezoneService)
    {
        _attendanceService = attendanceService;
        _context = context;
        _academicYearService = academicYearService;
        _timezoneService = timezoneService;
    }

    public AttendanceSummary Summary { get; set; } = new();
    public List<StudentAttendanceRecord> AttendanceRecords { get; set; } = new();
    public List<string> Grades { get; set; } = new();
    public List<string> Sections { get; set; } = new();
    public List<AcademicYear> AcademicYears { get; set; } = new();
    public AcademicYear? CurrentAcademicYear { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? SelectedDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? AcademicYearId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? GradeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SectionFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }
    public DateTime DisplayDate => SelectedDate ?? DateTime.Today;

    public async Task OnGetAsync()
    {
        // Load academic years
        CurrentAcademicYear = await _academicYearService.GetCurrentAcademicYearAsync();
        AcademicYears = await _academicYearService.GetAllAcademicYearsAsync(activeOnly: true);

        // Use current academic year if none selected
        if (!AcademicYearId.HasValue && CurrentAcademicYear != null)
        {
            AcademicYearId = CurrentAcademicYear.Id;
        }

        var targetDate = SelectedDate ?? DateTime.Today;

        // Load summary statistics (US0034-AC1, AC2)
        Summary = await _attendanceService.GetAttendanceSummaryAsync(
            targetDate, GradeFilter, SectionFilter);

        // Load attendance list (US0034-AC3)
        AttendanceRecords = await _attendanceService.GetAttendanceListAsync(
            targetDate, GradeFilter, SectionFilter, SearchTerm, StatusFilter, PageNumber, 50);

        // Get total count for pagination (US0034-AC6)
        TotalRecords = await _attendanceService.GetAttendanceCountAsync(
            targetDate, GradeFilter, SectionFilter, SearchTerm, StatusFilter);

        TotalPages = (int)Math.Ceiling(TotalRecords / 50.0);

        // Load filter options
        await LoadFiltersAsync();

        // Convert times to Philippines Time for display
        foreach (var record in AttendanceRecords)
        {
            // Times are already in UTC in the database, just mark them for display
            // The view will use the TimezoneService for formatting
        }
    }

    private async Task LoadFiltersAsync()
    {
        // Get unique grades
        Grades = await _context.Students
            .Where(s => s.IsActive)
            .Select(s => s.GradeLevel)
            .Distinct()
            .OrderBy(g => g)
            .ToListAsync();

        // Get unique sections
        Sections = await _context.Students
            .Where(s => s.IsActive)
            .Select(s => s.Section)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();
    }
}
