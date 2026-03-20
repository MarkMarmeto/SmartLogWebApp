namespace SmartLog.Web.Services;

/// <summary>
/// Service for attendance tracking and aggregation.
/// </summary>
public interface IAttendanceService
{
    /// <summary>
    /// Get attendance summary for a specific date.
    /// </summary>
    Task<AttendanceSummary> GetAttendanceSummaryAsync(DateTime date, string? gradeFilter = null, string? sectionFilter = null);

    /// <summary>
    /// Get detailed attendance list for a specific date.
    /// </summary>
    Task<List<StudentAttendanceRecord>> GetAttendanceListAsync(
        DateTime date,
        string? gradeFilter = null,
        string? sectionFilter = null,
        string? searchTerm = null,
        string? statusFilter = null,
        int pageNumber = 1,
        int pageSize = 50);

    /// <summary>
    /// Get total count for pagination.
    /// </summary>
    Task<int> GetAttendanceCountAsync(
        DateTime date,
        string? gradeFilter = null,
        string? sectionFilter = null,
        string? searchTerm = null,
        string? statusFilter = null);
}

/// <summary>
/// Attendance summary statistics.
/// </summary>
public class AttendanceSummary
{
    public int TotalEnrolled { get; set; }
    public int Present { get; set; }
    public int Absent { get; set; }
    public int Departed { get; set; }
    public decimal AttendanceRate { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Individual student attendance record.
/// </summary>
public class StudentAttendanceRecord
{
    public int StudentId { get; set; }
    public string StudentIdNumber { get; set; } = string.Empty;
    public string? LRN { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "Present", "Absent", "Departed"
    public DateTime? EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }
}
