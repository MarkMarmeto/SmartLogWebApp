using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLog.Web.Services;

namespace SmartLog.Web.Controllers.Api;

/// <summary>
/// REST API controller for attendance data.
/// Implements US0037 (Dashboard Auto-Refresh).
/// </summary>
[ApiController]
[Route("api/v1/attendance")]
[Produces("application/json")]
[Authorize(Policy = "CanViewStudents")]
public class AttendanceApiController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;
    private readonly ILogger<AttendanceApiController> _logger;

    public AttendanceApiController(
        IAttendanceService attendanceService,
        ILogger<AttendanceApiController> logger)
    {
        _attendanceService = attendanceService;
        _logger = logger;
    }

    /// <summary>
    /// Get attendance summary for auto-refresh.
    /// US0037: Provides data for dashboard auto-refresh.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? date = null,
        [FromQuery] string? grade = null,
        [FromQuery] string? section = null)
    {
        try
        {
            var targetDate = date ?? DateTime.Today;
            var summary = await _attendanceService.GetAttendanceSummaryAsync(targetDate, grade, section);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting attendance summary");
            return StatusCode(500, new { error = "Failed to load attendance data" });
        }
    }

    /// <summary>
    /// Get attendance list for auto-refresh.
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetList(
        [FromQuery] DateTime? date = null,
        [FromQuery] string? grade = null,
        [FromQuery] string? section = null,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            pageSize = Math.Clamp(pageSize, 1, 200);
            var targetDate = date ?? DateTime.Today;
            var records = await _attendanceService.GetAttendanceListAsync(
                targetDate, grade, section, search, status, page, pageSize);

            var totalCount = await _attendanceService.GetAttendanceCountAsync(
                targetDate, grade, section, search, status);

            return Ok(new
            {
                records,
                totalCount,
                pageNumber = page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting attendance list");
            return StatusCode(500, new { error = "Failed to load attendance list" });
        }
    }
}
