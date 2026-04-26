using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Pages.Admin.Reports;

/// <summary>
/// Student Attendance History page.
/// Implements US0048 (Student Attendance History).
/// </summary>
[Authorize(Policy = "CanViewStudents")]
public class StudentAttendanceHistoryModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public StudentAttendanceHistoryModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public Guid? StudentId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    public Student? Student { get; set; }
    public DateTime? ScanRetentionHorizon { get; private set; }
    public List<DailyAttendanceRecord> AttendanceHistory { get; set; } = new();
    public AttendanceSummary Summary { get; set; } = new();
    public List<Student> RecentStudents { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Load recent students for the dropdown
        RecentStudents = await _context.Students
            .Where(s => s.IsActive)
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .Take(100)
            .ToListAsync();

        ScanRetentionHorizon = await LoadScanRetentionHorizonAsync();

        if (StudentId.HasValue)
        {
            // US0048-AC1: Load student information
            Student = await _context.Students.FindAsync(StudentId.Value);

            if (Student != null)
            {
                // US0048-AC2: Default to last 30 days
                var endDate = EndDate ?? DateTime.Today;
                var startDate = StartDate ?? endDate.AddDays(-30);

                // Load attendance history
                await LoadAttendanceHistoryAsync(Student.Id, startDate, endDate);

                // Calculate summary statistics
                CalculateSummary(startDate, endDate);
            }
        }
    }

    private async Task<DateTime?> LoadScanRetentionHorizonAsync()
    {
        var policy = await _context.RetentionPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.EntityName == "Scan");
        return policy is { Enabled: true }
            ? DateTime.UtcNow.AddDays(-policy.RetentionDays)
            : null;
    }

    private async Task LoadAttendanceHistoryAsync(Guid studentId, DateTime startDate, DateTime endDate)
    {
        var start = startDate.Date;
        var end = endDate.Date.AddDays(1); // exclusive upper bound

        // Single query for entire date range instead of one query per day
        var allScans = await _context.Scans
            .Include(s => s.Device)
            .Where(s => s.StudentId == studentId
                && s.ScannedAt >= start
                && s.ScannedAt < end
                && s.Status == "ACCEPTED")
            .OrderBy(s => s.ScannedAt)
            .Select(s => new { s.ScanType, s.ScannedAt, DeviceName = s.Device != null ? s.Device.Name : null })
            .ToListAsync();

        // Group scans by date in memory
        var scansByDate = allScans.GroupBy(s => s.ScannedAt.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var history = new List<DailyAttendanceRecord>();

        for (var date = start; date <= endDate.Date; date = date.AddDays(1))
        {
            scansByDate.TryGetValue(date, out var scans);

            var entryScans = scans?.Where(s => s.ScanType == "ENTRY").ToList();
            var exitScans = scans?.Where(s => s.ScanType == "EXIT").ToList();

            var hasEntry = entryScans != null && entryScans.Any();
            var hasExit = exitScans != null && exitScans.Any();

            var status = hasEntry && hasExit ? "Departed" :
                         hasEntry ? "Present" :
                         "Absent";

            history.Add(new DailyAttendanceRecord
            {
                Date = date,
                DayOfWeek = date.ToString("dddd"),
                Status = status,
                EntryTime = hasEntry ? entryScans!.First().ScannedAt : null,
                ExitTime = hasExit ? exitScans!.Last().ScannedAt : null,
                EntryDevice = hasEntry ? entryScans!.First().DeviceName : null,
                ExitDevice = hasExit ? exitScans!.Last().DeviceName : null,
                IsWeekend = date.DayOfWeek == System.DayOfWeek.Saturday || date.DayOfWeek == System.DayOfWeek.Sunday
            });
        }

        AttendanceHistory = history;
    }

    private void CalculateSummary(DateTime startDate, DateTime endDate)
    {
        var schoolDays = AttendanceHistory.Count(d => !d.IsWeekend);
        var daysPresent = AttendanceHistory.Count(d => !d.IsWeekend && d.Status != "Absent");
        var daysAbsent = AttendanceHistory.Count(d => !d.IsWeekend && d.Status == "Absent");

        Summary = new AttendanceSummary
        {
            TotalDays = (endDate.Date - startDate.Date).Days + 1,
            SchoolDays = schoolDays,
            DaysPresent = daysPresent,
            DaysAbsent = daysAbsent,
            AttendanceRate = schoolDays > 0 ? Math.Round((daysPresent * 100.0) / schoolDays, 1) : 0,
            EarliestEntry = AttendanceHistory.Where(d => d.EntryTime.HasValue).Min(d => d.EntryTime),
            LatestEntry = AttendanceHistory.Where(d => d.EntryTime.HasValue).Max(d => d.EntryTime),
            AverageEntryTime = CalculateAverageTime(AttendanceHistory.Where(d => d.EntryTime.HasValue).Select(d => d.EntryTime!.Value).ToList())
        };
    }

    private TimeSpan? CalculateAverageTime(List<DateTime> times)
    {
        if (!times.Any()) return null;

        var avgTicks = (long)times.Average(t => t.TimeOfDay.Ticks);
        return TimeSpan.FromTicks(avgTicks);
    }

    public class DailyAttendanceRecord
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public string? EntryDevice { get; set; }
        public string? ExitDevice { get; set; }
        public bool IsWeekend { get; set; }
    }

    public class AttendanceSummary
    {
        public int TotalDays { get; set; }
        public int SchoolDays { get; set; }
        public int DaysPresent { get; set; }
        public int DaysAbsent { get; set; }
        public double AttendanceRate { get; set; }
        public DateTime? EarliestEntry { get; set; }
        public DateTime? LatestEntry { get; set; }
        public TimeSpan? AverageEntryTime { get; set; }
    }
}
