using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;

namespace SmartLog.Web.Pages.Admin.Reports;

/// <summary>
/// Monthly Attendance Report page.
/// Implements US0047 (Monthly Attendance Report).
/// </summary>
[Authorize(Policy = "CanViewStudents")]
public class MonthlyAttendanceReportModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public MonthlyAttendanceReportModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Month { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? GradeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SectionFilter { get; set; }

    public int ReportYear => Year ?? DateTime.Today.Year;
    public int ReportMonth => Month ?? DateTime.Today.Month;
    public string MonthName => new DateTime(ReportYear, ReportMonth, 1).ToString("MMMM yyyy");
    public List<DailyAttendanceSummary> DailySummaries { get; set; } = new();
    public MonthlyTotals Totals { get; set; } = new();
    public List<StudentMonthlyAttendance> StudentAttendance { get; set; } = new();

    public async Task OnGetAsync()
    {
        var monthStart = new DateTime(ReportYear, ReportMonth, 1);
        var monthEnd = monthStart.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(ReportYear, ReportMonth);

        // US0047-AC1: Calculate daily summaries for each day of the month
        for (int day = 1; day <= daysInMonth; day++)
        {
            var targetDate = new DateTime(ReportYear, ReportMonth, day);
            var summary = await CalculateDailySummaryAsync(targetDate);
            DailySummaries.Add(summary);
        }

        // US0047-AC2: Calculate monthly totals
        var schoolDays = DailySummaries.Count(d => d.DayOfWeek != "Saturday" && d.DayOfWeek != "Sunday");
        Totals = new MonthlyTotals
        {
            TotalEnrolled = DailySummaries.FirstOrDefault()?.TotalEnrolled ?? 0,
            SchoolDays = schoolDays,
            AverageAttendanceRate = schoolDays > 0
                ? Math.Round(DailySummaries
                    .Where(d => d.DayOfWeek != "Saturday" && d.DayOfWeek != "Sunday")
                    .Average(d => d.AttendanceRate), 1)
                : 0,
            TotalPresentDays = DailySummaries.Sum(d => d.Present + d.Departed),
            TotalAbsentDays = DailySummaries.Sum(d => d.Absent),
            HighestAttendance = DailySummaries.Any() ? DailySummaries.Max(d => d.Present + d.Departed) : 0,
            LowestAttendance = DailySummaries.Any() ? DailySummaries.Min(d => d.Present + d.Departed) : 0
        };

        // US0047-AC3: Calculate per-student monthly attendance
        await LoadStudentMonthlyAttendanceAsync(monthStart, monthEnd, schoolDays);
    }

    private async Task<DailyAttendanceSummary> CalculateDailySummaryAsync(DateTime date)
    {
        var dateOnly = date.Date;
        var nextDay = dateOnly.AddDays(1);

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

        var scans = await _context.Scans
            .Where(s => s.ScannedAt >= dateOnly && s.ScannedAt < nextDay && s.Status == "ACCEPTED")
            .GroupBy(s => new { s.StudentId, s.ScanType })
            .Select(g => new { g.Key.StudentId, g.Key.ScanType })
            .ToListAsync();

        var studentsWithEntry = scans.Where(s => s.ScanType == "ENTRY").Select(s => s.StudentId).Distinct().Count();
        var studentsWithExit = scans.Where(s => s.ScanType == "EXIT").Select(s => s.StudentId).Distinct().Count();

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
            AttendanceRate = attendanceRate,
            IsWeekend = date.DayOfWeek == System.DayOfWeek.Saturday || date.DayOfWeek == System.DayOfWeek.Sunday
        };
    }

    private async Task LoadStudentMonthlyAttendanceAsync(DateTime monthStart, DateTime monthEnd, int schoolDays)
    {
        var studentsQuery = _context.Students.Where(s => s.IsActive);

        if (!string.IsNullOrEmpty(GradeFilter))
        {
            studentsQuery = studentsQuery.Where(s => s.GradeLevel == GradeFilter);
        }

        if (!string.IsNullOrEmpty(SectionFilter))
        {
            studentsQuery = studentsQuery.Where(s => s.Section == SectionFilter);
        }

        var students = await studentsQuery
            .OrderBy(s => s.GradeLevel)
            .ThenBy(s => s.Section)
            .ThenBy(s => s.LastName)
            .Select(s => new { s.Id, s.StudentId, s.FirstName, s.LastName, s.GradeLevel, s.Section })
            .ToListAsync();

        var studentAttendanceList = new List<StudentMonthlyAttendance>();

        foreach (var student in students)
        {
            // Count days with ENTRY scans
            var daysPresent = await _context.Scans
                .Where(s => s.StudentId == student.Id
                    && s.ScannedAt >= monthStart
                    && s.ScannedAt < monthEnd
                    && s.ScanType == "ENTRY"
                    && s.Status == "ACCEPTED")
                .Select(s => s.ScannedAt.Date)
                .Distinct()
                .CountAsync();

            var daysAbsent = schoolDays - daysPresent;
            var attendanceRate = schoolDays > 0 ? Math.Round((daysPresent * 100.0) / schoolDays, 1) : 0;

            studentAttendanceList.Add(new StudentMonthlyAttendance
            {
                StudentId = student.StudentId,
                FullName = $"{student.FirstName} {student.LastName}",
                GradeLevel = student.GradeLevel,
                Section = student.Section,
                DaysPresent = daysPresent,
                DaysAbsent = daysAbsent,
                AttendanceRate = attendanceRate
            });
        }

        StudentAttendance = studentAttendanceList;
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
        public bool IsWeekend { get; set; }
    }

    public class MonthlyTotals
    {
        public int TotalEnrolled { get; set; }
        public int SchoolDays { get; set; }
        public double AverageAttendanceRate { get; set; }
        public int TotalPresentDays { get; set; }
        public int TotalAbsentDays { get; set; }
        public int HighestAttendance { get; set; }
        public int LowestAttendance { get; set; }
    }

    public class StudentMonthlyAttendance
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public int DaysPresent { get; set; }
        public int DaysAbsent { get; set; }
        public double AttendanceRate { get; set; }
    }
}
