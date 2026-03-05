using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;

namespace SmartLog.Web.Pages.Admin.Reports;

/// <summary>
/// Weekly Attendance Summary page.
/// Implements US0046 (Weekly Attendance Summary).
/// </summary>
[Authorize(Policy = "CanViewStudents")]
public class WeeklyAttendanceSummaryModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public WeeklyAttendanceSummaryModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? GradeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SectionFilter { get; set; }

    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public List<DailyAttendanceSummary> DailySummaries { get; set; } = new();
    public WeeklyTotals Totals { get; set; } = new();
    public List<Data.Entities.GradeLevel> GradeLevels { get; set; } = new();

    public async Task OnGetAsync()
    {
        GradeLevels = _context.GradeLevels
            .Where(g => g.IsActive)
            .OrderBy(g => g.SortOrder)
            .ToList();

        // US0046-AC1: Default to current week (Monday to Sunday)
        var referenceDate = StartDate ?? DateTime.Today;
        WeekStart = referenceDate.AddDays(-(int)referenceDate.DayOfWeek + (int)DayOfWeek.Monday);
        if (referenceDate.DayOfWeek == DayOfWeek.Sunday)
        {
            WeekStart = WeekStart.AddDays(-7); // If Sunday, go back to previous Monday
        }
        WeekEnd = WeekStart.AddDays(6);

        // US0046-AC2: Calculate daily summaries for each day of the week
        for (int i = 0; i < 7; i++)
        {
            var targetDate = WeekStart.AddDays(i);
            var summary = await CalculateDailySummaryAsync(targetDate);
            DailySummaries.Add(summary);
        }

        // US0046-AC3: Calculate weekly totals and averages
        Totals = new WeeklyTotals
        {
            TotalEnrolled = DailySummaries.FirstOrDefault()?.TotalEnrolled ?? 0,
            AverageDailyAttendance = DailySummaries.Any()
                ? (int)Math.Round(DailySummaries.Average(d => d.Present + d.Departed))
                : 0,
            TotalPresentDays = DailySummaries.Sum(d => d.Present),
            TotalAbsentDays = DailySummaries.Sum(d => d.Absent),
            AverageAttendanceRate = DailySummaries.Any()
                ? Math.Round(DailySummaries.Average(d => d.AttendanceRate), 1)
                : 0
        };
    }

    private async Task<DailyAttendanceSummary> CalculateDailySummaryAsync(DateTime date)
    {
        var dateOnly = date.Date;
        var nextDay = dateOnly.AddDays(1);

        // Get all active students
        var studentsQuery = _context.Students.Where(s => s.IsActive);

        if (!string.IsNullOrEmpty(GradeFilter))
        {
            studentsQuery = studentsQuery.Where(s => s.GradeLevel == GradeFilter);
        }

        if (!string.IsNullOrEmpty(SectionFilter))
        {
            studentsQuery = studentsQuery.Where(s => s.Section == SectionFilter);
        }

        var totalEnrolled = await studentsQuery.CountAsync();

        // Get scans for the day
        var scans = await _context.Scans
            .Where(s => s.ScannedAt >= dateOnly && s.ScannedAt < nextDay && s.Status == "ACCEPTED")
            .GroupBy(s => new { s.StudentId, s.ScanType })
            .Select(g => new { g.Key.StudentId, g.Key.ScanType })
            .ToListAsync();

        var studentsWithEntry = scans.Where(s => s.ScanType == "ENTRY").Select(s => s.StudentId).Distinct().Count();
        var studentsWithExit = scans.Where(s => s.ScanType == "EXIT").Select(s => s.StudentId).Distinct().Count();

        // Students who entered but haven't exited are still present
        var present = studentsWithEntry - studentsWithExit;
        if (present < 0) present = 0;

        var departed = studentsWithExit;
        var absent = totalEnrolled - studentsWithEntry;

        var attendanceRate = totalEnrolled > 0
            ? Math.Round((studentsWithEntry * 100.0) / totalEnrolled, 1)
            : 0;

        return new DailyAttendanceSummary
        {
            Date = date,
            DayOfWeek = date.ToString("dddd"),
            TotalEnrolled = totalEnrolled,
            Present = present,
            Absent = absent,
            Departed = departed,
            AttendanceRate = attendanceRate
        };
    }

    public class DailyAttendanceSummary
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; } = string.Empty;
        public int TotalEnrolled { get; set; }
        public int Present { get; set; }
        public int Absent { get; set; }
        public int Departed { get; set; }
        public double AttendanceRate { get; set; }
    }

    public class WeeklyTotals
    {
        public int TotalEnrolled { get; set; }
        public int AverageDailyAttendance { get; set; }
        public int TotalPresentDays { get; set; }
        public int TotalAbsentDays { get; set; }
        public double AverageAttendanceRate { get; set; }
    }
}
