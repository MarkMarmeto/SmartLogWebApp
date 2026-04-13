using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;

namespace SmartLog.Web.Services;

/// <summary>
/// Service for attendance tracking and aggregation.
/// Implements US0034 (School-Wide Attendance Dashboard).
/// </summary>
public class AttendanceService : IAttendanceService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(ApplicationDbContext context, ILogger<AttendanceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get attendance summary for a specific date.
    /// US0034-AC1, AC2: Calculate attendance statistics.
    /// </summary>
    public async Task<AttendanceSummary> GetAttendanceSummaryAsync(
        DateTime date,
        string? gradeFilter = null,
        string? sectionFilter = null)
    {
        var dateOnly = date.Date;
        var nextDay = dateOnly.AddDays(1);

        // Get active students (US0034-AC1: Total Enrolled)
        var studentsQuery = _context.Students
            .Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(gradeFilter))
        {
            studentsQuery = studentsQuery.Where(s => s.GradeLevel == gradeFilter);
        }

        if (!string.IsNullOrWhiteSpace(sectionFilter))
        {
            studentsQuery = studentsQuery.Where(s => s.Section == sectionFilter);
        }

        var totalEnrolled = await studentsQuery.CountAsync();

        // Get scans for the specified date
        var scans = await _context.Scans
            .Where(s => s.ScannedAt >= dateOnly && s.ScannedAt < nextDay && s.Status == "ACCEPTED")
            .Join(studentsQuery,
                scan => scan.StudentId,
                student => student.Id,
                (scan, student) => new { scan.StudentId, scan.ScanType })
            .ToListAsync();

        // Count students with ENTRY scans (Present)
        var studentsWithEntry = scans
            .Where(s => s.ScanType == "ENTRY")
            .Select(s => s.StudentId)
            .Distinct()
            .Count();

        // Count students with both ENTRY and EXIT scans (Departed)
        var studentsWithExit = scans
            .GroupBy(s => s.StudentId)
            .Where(g => g.Any(s => s.ScanType == "ENTRY") && g.Any(s => s.ScanType == "EXIT"))
            .Count();

        // Present = students with entry but no exit
        var present = studentsWithEntry - studentsWithExit;

        // Departed = students with both entry and exit
        var departed = studentsWithExit;

        // Absent = total - (present + departed)
        var absent = totalEnrolled - present - departed;

        // Calculate attendance rate (US0034-AC2)
        var attendanceRate = totalEnrolled > 0
            ? (decimal)(present + departed) / totalEnrolled * 100
            : 0;

        return new AttendanceSummary
        {
            TotalEnrolled = totalEnrolled,
            Present = present,
            Absent = absent,
            Departed = departed,
            AttendanceRate = Math.Round(attendanceRate, 1),
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Get detailed attendance list for a specific date.
    /// US0034-AC3: Student attendance list with pagination.
    /// Status filter is applied BEFORE pagination so counts are accurate.
    /// </summary>
    public async Task<List<StudentAttendanceRecord>> GetAttendanceListAsync(
        DateTime date,
        string? gradeFilter = null,
        string? sectionFilter = null,
        string? searchTerm = null,
        string? statusFilter = null,
        int pageNumber = 1,
        int pageSize = 50)
    {
        // Cap pageSize to prevent abuse
        pageSize = Math.Clamp(pageSize, 1, 200);

        var records = await BuildAttendanceRecordsAsync(date, gradeFilter, sectionFilter, searchTerm);

        // Apply status filter BEFORE pagination
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            records = records.Where(r => r.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Apply pagination AFTER status filter
        return records
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    /// <summary>
    /// Get total count for pagination, accounting for status filter.
    /// </summary>
    public async Task<int> GetAttendanceCountAsync(
        DateTime date,
        string? gradeFilter = null,
        string? sectionFilter = null,
        string? searchTerm = null,
        string? statusFilter = null)
    {
        // When no status filter, count is just the number of matching students
        if (string.IsNullOrWhiteSpace(statusFilter))
        {
            var studentsQuery = _context.Students.Where(s => s.IsActive);

            if (!string.IsNullOrWhiteSpace(gradeFilter))
                studentsQuery = studentsQuery.Where(s => s.GradeLevel == gradeFilter);

            if (!string.IsNullOrWhiteSpace(sectionFilter))
                studentsQuery = studentsQuery.Where(s => s.Section == sectionFilter);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var search = searchTerm.ToLower();
                studentsQuery = studentsQuery.Where(s =>
                    s.FirstName.ToLower().Contains(search) ||
                    s.LastName.ToLower().Contains(search) ||
                    s.StudentId.ToLower().Contains(search) ||
                    (s.LRN != null && s.LRN.Contains(search)));
            }

            return await studentsQuery.CountAsync();
        }

        // When status filter is active, we need to derive status to get accurate count
        var records = await BuildAttendanceRecordsAsync(date, gradeFilter, sectionFilter, searchTerm);
        return records.Count(r => r.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Builds attendance records with derived status for all matching students.
    /// Shared by GetAttendanceListAsync and GetAttendanceCountAsync to ensure consistency.
    /// </summary>
    private async Task<List<StudentAttendanceRecord>> BuildAttendanceRecordsAsync(
        DateTime date,
        string? gradeFilter = null,
        string? sectionFilter = null,
        string? searchTerm = null)
    {
        var dateOnly = date.Date;
        var nextDay = dateOnly.AddDays(1);

        // Get active students
        var studentsQuery = _context.Students
            .Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(gradeFilter))
            studentsQuery = studentsQuery.Where(s => s.GradeLevel == gradeFilter);

        if (!string.IsNullOrWhiteSpace(sectionFilter))
            studentsQuery = studentsQuery.Where(s => s.Section == sectionFilter);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.ToLower();
            studentsQuery = studentsQuery.Where(s =>
                s.FirstName.ToLower().Contains(search) ||
                s.LastName.ToLower().Contains(search) ||
                s.StudentId.ToLower().Contains(search) ||
                (s.LRN != null && s.LRN.Contains(search)));
        }

        // Load all matching students (needed to derive status before pagination)
        var students = await studentsQuery
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .Select(s => new
            {
                s.Id,
                s.StudentId,
                s.LRN,
                s.FirstName,
                s.LastName,
                s.GradeLevel,
                s.Section
            })
            .ToListAsync();

        var studentIds = students.Select(s => s.Id).ToList();

        // Get scans for these students on the specified date
        var scans = await _context.Scans
            .Where(s => studentIds.Contains(s.StudentId) &&
                       s.ScannedAt >= dateOnly &&
                       s.ScannedAt < nextDay &&
                       s.Status == "ACCEPTED")
            .OrderBy(s => s.ScannedAt)
            .ToListAsync();

        // Build attendance records with status derived from scans
        return students.Select(student =>
        {
            var studentScans = scans.Where(s => s.StudentId == student.Id).ToList();
            var entryScans = studentScans.Where(s => s.ScanType == "ENTRY").ToList();
            var exitScans = studentScans.Where(s => s.ScanType == "EXIT").ToList();

            string status;
            DateTime? entryTime = null;
            DateTime? exitTime = null;

            if (entryScans.Any())
            {
                entryTime = entryScans.First().ScannedAt;

                if (exitScans.Any(e => e.ScannedAt > entryTime))
                {
                    status = "Departed";
                    exitTime = exitScans.Where(e => e.ScannedAt > entryTime).First().ScannedAt;
                }
                else
                {
                    status = "Present";
                }
            }
            else
            {
                status = "Absent";
            }

            return new StudentAttendanceRecord
            {
                StudentId = student.Id,
                StudentIdNumber = student.StudentId,
                LRN = student.LRN,
                FullName = $"{student.FirstName} {student.LastName}",
                GradeLevel = student.GradeLevel,
                Section = student.Section,
                Status = status,
                EntryTime = entryTime,
                ExitTime = exitTime
            };
        }).ToList();
    }
}
