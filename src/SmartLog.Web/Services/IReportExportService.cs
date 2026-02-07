namespace SmartLog.Web.Services;

/// <summary>
/// Service for exporting reports to various formats (PDF, Excel).
/// Implements US0049 (Report Export).
/// </summary>
public interface IReportExportService
{
    /// <summary>
    /// Export daily attendance report to Excel.
    /// </summary>
    Task<byte[]> ExportDailyAttendanceToExcelAsync(DateTime date, string? grade, string? section);

    /// <summary>
    /// Export daily attendance report to PDF.
    /// </summary>
    Task<byte[]> ExportDailyAttendanceToPdfAsync(DateTime date, string? grade, string? section);

    /// <summary>
    /// Export weekly attendance summary to Excel.
    /// </summary>
    Task<byte[]> ExportWeeklyAttendanceToExcelAsync(DateTime weekStart, string? grade, string? section);

    /// <summary>
    /// Export weekly attendance summary to PDF.
    /// </summary>
    Task<byte[]> ExportWeeklyAttendanceToPdfAsync(DateTime weekStart, string? grade, string? section);

    /// <summary>
    /// Export monthly attendance report to Excel.
    /// </summary>
    Task<byte[]> ExportMonthlyAttendanceToExcelAsync(int year, int month, string? grade, string? section);

    /// <summary>
    /// Export monthly attendance report to PDF.
    /// </summary>
    Task<byte[]> ExportMonthlyAttendanceToPdfAsync(int year, int month, string? grade, string? section);

    /// <summary>
    /// Export student attendance history to Excel.
    /// </summary>
    Task<byte[]> ExportStudentHistoryToExcelAsync(int studentId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Export student attendance history to PDF.
    /// </summary>
    Task<byte[]> ExportStudentHistoryToPdfAsync(int studentId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Export audit logs to Excel.
    /// </summary>
    Task<byte[]> ExportAuditLogsToExcelAsync(DateTime? startDate, DateTime? endDate, string? action, string? userId, string? searchTerm);
}
