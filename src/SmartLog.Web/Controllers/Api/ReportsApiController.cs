using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLog.Web.Services;

namespace SmartLog.Web.Controllers.Api;

/// <summary>
/// API controller for report exports.
/// Implements US0049 (Report Export - PDF/Excel).
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = "CanViewStudents")]
public class ReportsApiController : ControllerBase
{
    private readonly IReportExportService _exportService;
    private readonly ILogger<ReportsApiController> _logger;

    public ReportsApiController(
        IReportExportService exportService,
        ILogger<ReportsApiController> logger)
    {
        _exportService = exportService;
        _logger = logger;
    }

    /// <summary>
    /// Export daily attendance report.
    /// </summary>
    [HttpGet("daily/export")]
    public async Task<IActionResult> ExportDailyAttendance(
        [FromQuery] DateTime? date,
        [FromQuery] string? grade,
        [FromQuery] string? section,
        [FromQuery] string format = "csv")
    {
        try
        {
            var targetDate = date ?? DateTime.Today;
            byte[] fileData;
            string contentType;
            string fileName;

            if (format.ToLower() == "pdf" || format.ToLower() == "html")
            {
                // US0049-AC2: PDF export (via HTML)
                fileData = await _exportService.ExportDailyAttendanceToPdfAsync(targetDate, grade, section);
                contentType = "text/html";
                fileName = $"daily-attendance-{targetDate:yyyy-MM-dd}.html";
            }
            else
            {
                // US0049-AC1: Excel export (CSV)
                fileData = await _exportService.ExportDailyAttendanceToExcelAsync(targetDate, grade, section);
                contentType = "text/csv";
                fileName = $"daily-attendance-{targetDate:yyyy-MM-dd}.csv";
            }

            return File(fileData, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting daily attendance report");
            return StatusCode(500, new { error = "Failed to export report" });
        }
    }

    /// <summary>
    /// Export weekly attendance summary.
    /// </summary>
    [HttpGet("weekly/export")]
    public async Task<IActionResult> ExportWeeklyAttendance(
        [FromQuery] DateTime? startDate,
        [FromQuery] string? grade,
        [FromQuery] string? section,
        [FromQuery] string format = "csv")
    {
        try
        {
            var weekStart = startDate ?? DateTime.Today;
            byte[] fileData;
            string contentType;
            string fileName;

            if (format.ToLower() == "pdf" || format.ToLower() == "html")
            {
                fileData = await _exportService.ExportWeeklyAttendanceToPdfAsync(weekStart, grade, section);
                contentType = "text/html";
                fileName = $"weekly-attendance-{weekStart:yyyy-MM-dd}.html";
            }
            else
            {
                fileData = await _exportService.ExportWeeklyAttendanceToExcelAsync(weekStart, grade, section);
                contentType = "text/csv";
                fileName = $"weekly-attendance-{weekStart:yyyy-MM-dd}.csv";
            }

            return File(fileData, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting weekly attendance summary");
            return StatusCode(500, new { error = "Failed to export report" });
        }
    }

    /// <summary>
    /// Export monthly attendance report.
    /// </summary>
    [HttpGet("monthly/export")]
    public async Task<IActionResult> ExportMonthlyAttendance(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] string? grade,
        [FromQuery] string? section,
        [FromQuery] string format = "csv")
    {
        try
        {
            var targetYear = year ?? DateTime.Today.Year;
            var targetMonth = month ?? DateTime.Today.Month;
            byte[] fileData;
            string contentType;
            string fileName;

            if (format.ToLower() == "pdf" || format.ToLower() == "html")
            {
                fileData = await _exportService.ExportMonthlyAttendanceToPdfAsync(targetYear, targetMonth, grade, section);
                contentType = "text/html";
                fileName = $"monthly-attendance-{targetYear}-{targetMonth:D2}.html";
            }
            else
            {
                fileData = await _exportService.ExportMonthlyAttendanceToExcelAsync(targetYear, targetMonth, grade, section);
                contentType = "text/csv";
                fileName = $"monthly-attendance-{targetYear}-{targetMonth:D2}.csv";
            }

            return File(fileData, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting monthly attendance report");
            return StatusCode(500, new { error = "Failed to export report" });
        }
    }

    /// <summary>
    /// Export student attendance history.
    /// </summary>
    [HttpGet("student/{studentId}/export")]
    public async Task<IActionResult> ExportStudentHistory(
        int studentId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string format = "csv")
    {
        try
        {
            var end = endDate ?? DateTime.Today;
            var start = startDate ?? end.AddDays(-30);
            byte[] fileData;
            string contentType;
            string fileName;

            if (format.ToLower() == "pdf" || format.ToLower() == "html")
            {
                fileData = await _exportService.ExportStudentHistoryToPdfAsync(studentId, start, end);
                contentType = "text/html";
                fileName = $"student-{studentId}-history-{start:yyyy-MM-dd}-to-{end:yyyy-MM-dd}.html";
            }
            else
            {
                fileData = await _exportService.ExportStudentHistoryToExcelAsync(studentId, start, end);
                contentType = "text/csv";
                fileName = $"student-{studentId}-history-{start:yyyy-MM-dd}-to-{end:yyyy-MM-dd}.csv";
            }

            return File(fileData, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting student attendance history");
            return StatusCode(500, new { error = "Failed to export report" });
        }
    }

    /// <summary>
    /// Export audit logs.
    /// </summary>
    [HttpGet("audit-logs/export")]
    [Authorize(Policy = "RequireSuperAdmin")]
    public async Task<IActionResult> ExportAuditLogs(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? action,
        [FromQuery] string? userId,
        [FromQuery] string? searchTerm)
    {
        try
        {
            var fileData = await _exportService.ExportAuditLogsToExcelAsync(startDate, endDate, action, userId, searchTerm);
            var fileName = $"audit-logs-{DateTime.Now:yyyy-MM-dd-HHmmss}.csv";
            return File(fileData, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs");
            return StatusCode(500, new { error = "Failed to export audit logs" });
        }
    }
}
