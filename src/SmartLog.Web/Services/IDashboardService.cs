namespace SmartLog.Web.Services;

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync();
    Task<List<AttendanceTrendPoint>> GetAttendanceTrendAsync(int days);
    Task<List<GradeAttendance>> GetAttendanceByGradeAsync(DateTime date);
    Task<List<WeekdayAttendance>> GetAttendanceByWeekdayAsync(int weeks);
    Task<List<RecentActivity>> GetRecentActivityAsync(int count);
}

public class DashboardSummary
{
    public int TotalStudents { get; set; }
    public int TotalFaculty { get; set; }
    public decimal TodayAttendanceRate { get; set; }
    public int TotalSections { get; set; }
    public int StudentChange { get; set; }
    public int FacultyChange { get; set; }
    public int TodayPresent { get; set; }
    public int TodayTotal { get; set; }
}

public class AttendanceTrendPoint
{
    public DateTime Date { get; set; }
    public decimal AttendanceRate { get; set; }
    public int PresentCount { get; set; }
    public int TotalEnrolled { get; set; }
}

public class GradeAttendance
{
    public string GradeLevel { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public decimal AttendanceRate { get; set; }
    public int PresentCount { get; set; }
    public int TotalEnrolled { get; set; }
}

public class WeekdayAttendance
{
    public string DayOfWeek { get; set; } = string.Empty;
    public decimal AverageRate { get; set; }
    public decimal CurrentWeekRate { get; set; }
}

public class RecentActivity
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
}
