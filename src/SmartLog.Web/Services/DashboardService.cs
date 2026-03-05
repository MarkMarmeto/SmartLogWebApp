using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<DashboardService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<DashboardSummary> GetSummaryAsync()
    {
        var today = DateTime.UtcNow.Date;
        var lastMonth = today.AddMonths(-1);

        var totalStudents = await _context.Students.CountAsync(s => s.IsActive);
        var totalFaculty = await _context.Faculties.CountAsync(f => f.IsActive);
        var totalSections = await _context.Sections.CountAsync(s => s.IsActive);

        // Students added this month vs last month
        var studentsThisMonth = await _context.Students
            .CountAsync(s => s.CreatedAt >= today.AddDays(-30));
        var studentsLastMonth = await _context.Students
            .CountAsync(s => s.CreatedAt >= today.AddDays(-60) && s.CreatedAt < today.AddDays(-30));
        var studentChange = studentsThisMonth - studentsLastMonth;

        var facultyThisMonth = await _context.Faculties
            .CountAsync(f => f.CreatedAt >= today.AddDays(-30));
        var facultyLastMonth = await _context.Faculties
            .CountAsync(f => f.CreatedAt >= today.AddDays(-60) && f.CreatedAt < today.AddDays(-30));
        var facultyChange = facultyThisMonth - facultyLastMonth;

        // Today's attendance
        var todayScans = await _context.Scans
            .Where(s => s.ScannedAt.Date == today && s.ScanType == "ENTRY" && s.Status == "ACCEPTED")
            .Select(s => s.StudentId)
            .Distinct()
            .CountAsync();

        decimal todayRate = 0;
        if (totalStudents > 0)
        {
            todayRate = Math.Round((decimal)todayScans / totalStudents * 100, 1);
        }

        return new DashboardSummary
        {
            TotalStudents = totalStudents,
            TotalFaculty = totalFaculty,
            TodayAttendanceRate = todayRate,
            TotalSections = totalSections,
            StudentChange = studentChange,
            FacultyChange = facultyChange,
            TodayPresent = todayScans,
            TodayTotal = totalStudents
        };
    }

    public async Task<List<AttendanceTrendPoint>> GetAttendanceTrendAsync(int days)
    {
        var result = new List<AttendanceTrendPoint>();
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddDays(-days);

        var totalStudents = await _context.Students.CountAsync(s => s.IsActive);
        if (totalStudents == 0)
            return result;

        // Get all accepted entry scans in the date range
        var dailyScans = await _context.Scans
            .Where(s => s.ScannedAt.Date >= startDate && s.ScannedAt.Date <= today &&
                        s.ScanType == "ENTRY" && s.Status == "ACCEPTED")
            .GroupBy(s => s.ScannedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                PresentCount = g.Select(s => s.StudentId).Distinct().Count()
            })
            .ToListAsync();

        var scanDict = dailyScans.ToDictionary(d => d.Date, d => d.PresentCount);

        for (var date = startDate; date <= today; date = date.AddDays(1))
        {
            // Skip weekends
            if (date.DayOfWeek == System.DayOfWeek.Saturday || date.DayOfWeek == System.DayOfWeek.Sunday)
                continue;

            var present = scanDict.GetValueOrDefault(date, 0);
            result.Add(new AttendanceTrendPoint
            {
                Date = date,
                PresentCount = present,
                TotalEnrolled = totalStudents,
                AttendanceRate = Math.Round((decimal)present / totalStudents * 100, 1)
            });
        }

        return result;
    }

    public async Task<List<GradeAttendance>> GetAttendanceByGradeAsync(DateTime date)
    {
        var result = new List<GradeAttendance>();
        var targetDate = date.Date;

        // Get students grouped by grade level
        var gradeGroups = await _context.Students
            .Where(s => s.IsActive)
            .GroupBy(s => s.GradeLevel)
            .Select(g => new
            {
                GradeLevel = g.Key,
                TotalEnrolled = g.Count(),
                StudentIds = g.Select(s => s.Id).ToList()
            })
            .ToListAsync();

        // Get today's entry scans
        var presentStudentIds = await _context.Scans
            .Where(s => s.ScannedAt.Date == targetDate && s.ScanType == "ENTRY" && s.Status == "ACCEPTED")
            .Select(s => s.StudentId)
            .Distinct()
            .ToListAsync();

        var presentSet = new HashSet<int>(presentStudentIds);

        foreach (var group in gradeGroups.OrderBy(g => GetGradeSortOrder(g.GradeLevel)))
        {
            var presentCount = group.StudentIds.Count(id => presentSet.Contains(id));
            var rate = group.TotalEnrolled > 0
                ? Math.Round((decimal)presentCount / group.TotalEnrolled * 100, 1)
                : 0;

            result.Add(new GradeAttendance
            {
                GradeLevel = group.GradeLevel,
                GradeName = $"Grade {group.GradeLevel}",
                PresentCount = presentCount,
                TotalEnrolled = group.TotalEnrolled,
                AttendanceRate = rate
            });
        }

        return result;
    }

    public async Task<List<WeekdayAttendance>> GetAttendanceByWeekdayAsync(int weeks)
    {
        var result = new List<WeekdayAttendance>();
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddDays(-weeks * 7);

        var totalStudents = await _context.Students.CountAsync(s => s.IsActive);
        if (totalStudents == 0)
            return result;

        var dailyScans = await _context.Scans
            .Where(s => s.ScannedAt.Date >= startDate && s.ScannedAt.Date <= today &&
                        s.ScanType == "ENTRY" && s.Status == "ACCEPTED")
            .GroupBy(s => s.ScannedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                PresentCount = g.Select(s => s.StudentId).Distinct().Count()
            })
            .ToListAsync();

        // Find current week's Monday
        var currentWeekStart = today.AddDays(-(int)today.DayOfWeek + (int)System.DayOfWeek.Monday);
        if (today.DayOfWeek == System.DayOfWeek.Sunday)
            currentWeekStart = currentWeekStart.AddDays(-7);

        var weekdays = new[] { System.DayOfWeek.Monday, System.DayOfWeek.Tuesday,
            System.DayOfWeek.Wednesday, System.DayOfWeek.Thursday, System.DayOfWeek.Friday };

        foreach (var day in weekdays)
        {
            var dayScans = dailyScans.Where(d => d.Date.DayOfWeek == day).ToList();
            var currentWeekDay = dayScans.FirstOrDefault(d => d.Date >= currentWeekStart);

            var avgRate = dayScans.Count > 0
                ? Math.Round(dayScans.Average(d => (decimal)d.PresentCount / totalStudents * 100), 1)
                : 0;

            var currentRate = currentWeekDay != null
                ? Math.Round((decimal)currentWeekDay.PresentCount / totalStudents * 100, 1)
                : 0;

            result.Add(new WeekdayAttendance
            {
                DayOfWeek = day.ToString(),
                AverageRate = avgRate,
                CurrentWeekRate = currentRate
            });
        }

        return result;
    }

    public async Task<List<RecentActivity>> GetRecentActivityAsync(int count)
    {
        var activities = await _context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .Select(a => new RecentActivity
            {
                Timestamp = a.Timestamp,
                Action = a.Action,
                UserName = a.PerformedByUser != null ? a.PerformedByUser.UserName! : "System",
                Details = a.Details ?? a.Action,
                LinkUrl = GetLinkForAction(a.Action)
            })
            .ToListAsync();

        return activities;
    }

    private static string? GetLinkForAction(string action)
    {
        return action switch
        {
            "StudentCreated" or "BulkStudentImport" => "/Admin/Students",
            "CreateFaculty" or "BulkFacultyImport" => "/Admin/Faculty",
            "UserCreated" or "UserDeactivated" or "UserReactivated" => "/Admin/Users",
            "PasswordChanged" or "PasswordReset" => "/Admin/Users",
            "LoginSuccess" or "LoginFailed" or "AccountLocked" => "/Admin/AuditLogs",
            _ => null
        };
    }

    private static int GetGradeSortOrder(string gradeLevel)
    {
        if (int.TryParse(gradeLevel, out var grade)) return grade;
        return 99;
    }
}
